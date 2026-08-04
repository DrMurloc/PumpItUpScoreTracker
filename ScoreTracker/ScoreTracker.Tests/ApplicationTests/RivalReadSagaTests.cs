using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The head-to-head tally. What counts as a comparable result is the whole question here:
///     the ledger holds scoreless break rows, so "every best attempt" is not "every score".
/// </summary>
public sealed class RivalReadSagaTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Played = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Guid _edgeId = Guid.NewGuid();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRivalRepository> _rivals = new();
    private readonly Guid _rival = Guid.NewGuid();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IUserReader> _users = new();

    public RivalReadSagaTests()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_me).Build());
        _rivals.Setup(r => r.GetEdge(_edgeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RivalEdge(_edgeId, _me, _rival, null, Added));
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserBuilder().WithId(_rival).Build() }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(null, Array.Empty<OfficialTagScore>()));
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>().AsEnumerable());
    }

    private RivalReadSaga Saga() => new(_rivals.Object,
        new RivalSubjectResolver(_users.Object, _mediator.Object),
        new RivalScoreReader(_scores.Object, _mediator.Object),
        new RivalAudienceReader(_mediator.Object), _scores.Object, _users.Object, _mediator.Object,
        _currentUser.Object);

    private void MyBestsAre(params RecordedPhoenixScore[] records) =>
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), _me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private void TheirBestsAre(params UserPhoenixScore[] records) =>
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private static RecordedPhoenixScore Mine(Guid chart, int? score, bool broken = false) =>
        new(chart, score == null ? null : score.Value, null, broken, Played);

    private static UserPhoenixScore Theirs(Guid rival, Guid chart, int score, bool broken = false) =>
        new(rival, chart, "RIVAL", score, null, broken);

    private Task<RivalHeadToHeadRecord?> Compare() =>
        Saga().Handle(new GetRivalHeadToHeadQuery(MixEnum.Phoenix, _edgeId), CancellationToken.None);

    /// <summary>
    ///     The reported crash. A break with no score at all is a real row in the ledger, and
    ///     reading it as a number threw before it could be compared to anything.
    /// </summary>
    [Fact]
    public async Task AScorelessBreakIsNotAComparableResult()
    {
        MyBestsAre(Mine(ChartA, null), Mine(ChartB, 980_000));
        TheirBestsAre(Theirs(_rival, ChartA, 950_000), Theirs(_rival, ChartB, 970_000));

        var result = await Compare();

        Assert.NotNull(result);
        // ChartA still lists — their score is real — but with nothing of yours beside it, so it
        // is not a loss.
        Assert.Null(result!.Rows.Single(r => r.ChartId == ChartA).YourScore);
        Assert.Equal(1, result.Shared);
        Assert.Equal(1, result.YouAhead);
        Assert.Equal(0, result.TheyAhead);
    }

    /// <summary>
    ///     A score set on a run that broke is not a result you hold, on either side of the table.
    ///     An official placement is a completed run by construction, so counting only OUR breaks
    ///     out would hand every ghost comparison a free win.
    /// </summary>
    [Fact]
    public async Task ABrokenRunCountsForNeitherSide()
    {
        MyBestsAre(Mine(ChartA, 999_000, broken: true), Mine(ChartB, 900_000));
        TheirBestsAre(Theirs(_rival, ChartA, 800_000), Theirs(_rival, ChartB, 999_000, broken: true));

        var result = await Compare();

        Assert.NotNull(result);
        // Your 999k on A broke, so it does not beat their 800k.
        Assert.Null(result!.Rows.Single(r => r.ChartId == ChartA).YourScore);
        // Their 999k on B broke, so B drops out entirely rather than counting against you.
        Assert.DoesNotContain(result.Rows, r => r.ChartId == ChartB);
        Assert.Equal(0, result.Shared);
        Assert.Equal(0, result.YouAhead);
        Assert.Equal(0, result.TheyAhead);
    }

    [Fact]
    public async Task TheTallyCountsOnlyChartsBothOfYouHaveScored()
    {
        MyBestsAre(Mine(ChartA, 990_000), Mine(ChartB, 900_000));
        TheirBestsAre(Theirs(_rival, ChartA, 980_000), Theirs(_rival, ChartB, 950_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Shared);
        Assert.Equal(1, result.YouAhead);
        Assert.Equal(1, result.TheyAhead);
    }

    /// <summary>A rival with nothing comparable is an empty table, not a crash and not a loss.</summary>
    [Fact]
    public async Task NothingComparableYieldsAnEmptyTable()
    {
        MyBestsAre(Mine(ChartA, null), Mine(ChartB, null));
        TheirBestsAre(Theirs(_rival, ChartA, 950_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Empty(result!.Rows);
        Assert.Equal(0, result.Shared);
    }
}
