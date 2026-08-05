using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RivalScoreReaderTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sealed = new(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IScoreReader> _scores = new();

    private RivalScoreReader Reader() => new(_scores.Object, _mediator.Object);

    private static RivalSubject Site(Guid userId, string name) =>
        new(Guid.NewGuid(), userId, null, name, null, false,
            RivalCapabilities.LiveScores | RivalCapabilities.FolderCompare, Added);

    private static RivalSubject Ghost(string tag) =>
        new(Guid.NewGuid(), null, tag, tag, null, true, RivalCapabilities.OfficialStandings, Added);

    private void SiteScoresAre(params UserPhoenixScore[] scores) =>
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);

    private void OfficialScoresAre(DateTimeOffset? asOf, params OfficialTagScore[] scores) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(asOf, scores));

    [Fact]
    public async Task MergesLiveAndBoardScoresOntoOneChartRankedTogether()
    {
        var siteUser = Guid.NewGuid();
        SiteScoresAre(new UserPhoenixScore(siteUser, Chart, "ERRLENA", 993_000, null, false));
        OfficialScoresAre(Sealed, new OfficialTagScore("FRANKEZA#9606", Chart, 1, 998_440));

        var result = await Reader().Read(new[] { Site(siteUser, "ERRLENA"), Ghost("FRANKEZA#9606") },
            MixEnum.Phoenix, new[] { Chart }, CancellationToken.None);

        var rows = result.ByChart[Chart];
        Assert.Equal(2, rows.Count);
        // Ranked together, highest first — a ghost is as close to a real rival as the data allows.
        Assert.Equal("FRANKEZA#9606", rows[0].DisplayName);
        Assert.Equal(RivalScoreSource.Official, rows[0].Source);
        Assert.Equal(RivalScoreSource.Site, rows[1].Source);
    }

    /// <summary>
    ///     The board half is up to a week old and sits beside live numbers. A caller that cannot
    ///     say when would be asserting a standing it does not have.
    /// </summary>
    [Fact]
    public async Task CarriesTheSnapshotInstantWheneverABoardScoreIsIncluded()
    {
        OfficialScoresAre(Sealed, new OfficialTagScore("FRANKEZA#9606", Chart, 1, 998_440));
        SiteScoresAre();

        var result = await Reader().Read(new[] { Ghost("FRANKEZA#9606") }, MixEnum.Phoenix, new[] { Chart },
            CancellationToken.None);

        Assert.Equal(Sealed, result.OfficialAsOf);
    }

    /// <summary>No ghosts, no snapshot read — and nothing to footnote.</summary>
    [Fact]
    public async Task SkipsTheMirrorEntirelyWhenEveryRivalIsASiteUser()
    {
        var siteUser = Guid.NewGuid();
        SiteScoresAre(new UserPhoenixScore(siteUser, Chart, "ERRLENA", 993_000, null, false));

        var result = await Reader().Read(new[] { Site(siteUser, "ERRLENA") }, MixEnum.Phoenix,
            new[] { Chart }, CancellationToken.None);

        Assert.Null(result.OfficialAsOf);
        _mediator.Verify(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Three hundred rivals across a session's charts is the shape this gets exercised in, so
    ///     each source is read once rather than once per rival.
    /// </summary>
    [Fact]
    public async Task ReadsEachSourceOnceRegardlessOfRivalCount()
    {
        var users = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();
        var tags = Enumerable.Range(0, 100).Select(i => $"TAG{i}").ToArray();
        SiteScoresAre();
        OfficialScoresAre(Sealed);

        await Reader().Read(
            users.Select(u => Site(u, "x")).Concat(tags.Select(Ghost)).ToArray(),
            MixEnum.Phoenix, new[] { Chart }, CancellationToken.None);

        _scores.Verify(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoRivalsMeansNoReadsAtAll()
    {
        var result = await Reader().Read(Array.Empty<RivalSubject>(), MixEnum.Phoenix, new[] { Chart },
            CancellationToken.None);

        Assert.Empty(result.ByChart);
        _scores.VerifyNoOtherCalls();
    }
}
