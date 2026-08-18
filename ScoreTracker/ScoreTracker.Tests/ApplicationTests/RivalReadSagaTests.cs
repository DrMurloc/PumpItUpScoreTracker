using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
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
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The head-to-head tally. What counts as a comparable result is the whole question here:
///     the ledger holds scoreless break rows, so "every best attempt" is not "every score" —
///     and a chart only one of you holds is a row with one side empty, never a loss.
/// </summary>
public sealed class RivalReadSagaTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Played = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ICommunityReader> _communities = new();
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
        _rivals.Setup(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalEdge>());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserBuilder().WithId(_rival).Build() }.AsEnumerable());
        _users.Setup(u => u.GetUser(_rival, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(_rival).WithName("RIVAL").WithIsPublic(true).Build());
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(null, Array.Empty<OfficialTagScore>()));
        _communities.Setup(c => c.GetUserCommunityMembers(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Name, IReadOnlyList<Guid>>());
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
    }

    private RivalReadSaga Saga() => new(_rivals.Object,
        new RivalSubjectResolver(_users.Object, _mediator.Object),
        new RivalScoreReader(_scores.Object, _mediator.Object),
        new PlayerVisibilityReader(_communities.Object, _rivals.Object), _scores.Object, _users.Object,
        _charts.Object, _mediator.Object, _currentUser.Object);

    private void MyBestsAre(params RecordedPhoenixScore[] records) =>
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), _me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private void TheirBestsAre(params RecordedPhoenixScore[] records) =>
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), _rival, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private void FolderHolds(ChartType type, int level, params Guid[] chartIds) =>
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), DifficultyLevel.From(level), type,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chartIds.Select(id =>
                new ChartBuilder().WithId(id).WithType(type).WithLevel(level).Build()).ToArray());

    private static RecordedPhoenixScore Score(Guid chart, int? score, bool broken = false,
        PhoenixPlate? plate = null) =>
        new(chart, score == null ? null : score.Value, plate, broken, Played);

    private Task<RivalHeadToHeadRecord?> Compare() =>
        Saga().Handle(new GetPlayerHeadToHeadQuery(MixEnum.Phoenix, _rival), CancellationToken.None);

    /// <summary>
    ///     Naming a folder narrows the comparison to that folder's chart list — every chart in it
    ///     that either of you has scored, not just the ones you both hold.
    /// </summary>
    [Fact]
    public async Task NamingAFolderComparesOnlyThatFolder()
    {
        MyBestsAre(Score(ChartA, 990_000), Score(ChartB, 900_000));
        TheirBestsAre(Score(ChartA, 980_000), Score(ChartB, 999_000));
        FolderHolds(ChartType.Single, 21, ChartA);

        var result = await Saga().Handle(
            new GetPlayerHeadToHeadQuery(MixEnum.Phoenix, _rival, ChartType.Single, DifficultyLevel.From(21)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new[] { ChartA }, result!.Rows.Select(r => r.ChartId));
        Assert.Equal(1, result.YouAhead);
    }

    /// <summary>Both sides carry the plate so the row can print the same score vocabulary every other board uses.</summary>
    [Fact]
    public async Task ARowCarriesBothPlates()
    {
        MyBestsAre(Score(ChartA, 1_000_000, plate: PhoenixPlate.PerfectGame));
        TheirBestsAre(Score(ChartA, 995_000, plate: PhoenixPlate.SuperbGame));

        var result = await Compare();

        var row = result!.Rows.Single();
        Assert.Equal(PhoenixPlate.PerfectGame, row.YourPlate);
        Assert.Equal(PhoenixPlate.SuperbGame, row.TheirPlate);
    }

    /// <summary>
    ///     A break with no score at all is a real row in the ledger, and reading it as a number
    ///     threw before it could be compared to anything. It is not a result you hold: the chart
    ///     becomes a row only they have, and it is emphatically not a loss.
    /// </summary>
    [Fact]
    public async Task AScorelessBreakIsNotAComparableResult()
    {
        MyBestsAre(Score(ChartA, null), Score(ChartB, 980_000));
        TheirBestsAre(Score(ChartA, 950_000), Score(ChartB, 970_000));

        var result = await Compare();

        Assert.NotNull(result);
        var a = result!.Rows.Single(r => r.ChartId == ChartA);
        Assert.Null(a.YourScore);
        Assert.Equal(950_000, a.TheirScore);
        Assert.Equal(1, result.Shared);
        Assert.Equal(1, result.YouAhead);
        Assert.Equal(0, result.TheyAhead);
        Assert.Equal(1, result.OnlyThem);
        Assert.Equal(0, result.OnlyYou);
    }

    /// <summary>
    ///     A score set on a run that broke is not a result you hold, on either side of the table.
    ///     An official placement is a completed run by construction, so counting only OUR breaks
    ///     out would hand every ghost comparison a free win.
    /// </summary>
    [Fact]
    public async Task ABrokenRunCountsForNeitherSide()
    {
        MyBestsAre(Score(ChartA, 999_000, broken: true), Score(ChartB, 900_000));
        TheirBestsAre(Score(ChartA, 800_000), Score(ChartB, 999_000, broken: true));

        var result = await Compare();

        Assert.NotNull(result);
        // Your 999k on A broke, so A is a chart only they hold — it cannot beat their 800k, and
        // their 800k cannot beat you either. Their 999k on B broke, so B is yours alone.
        Assert.Equal(0, result!.Shared);
        Assert.Equal(0, result.YouAhead);
        Assert.Equal(0, result.TheyAhead);
        Assert.Equal(1, result.OnlyThem);
        Assert.Equal(1, result.OnlyYou);
        Assert.Null(result.Rows.Single(r => r.ChartId == ChartA).YourScore);
        Assert.Null(result.Rows.Single(r => r.ChartId == ChartB).TheirScore);
    }

    [Fact]
    public async Task TheTallyCountsOnlyChartsBothOfYouHaveScored()
    {
        MyBestsAre(Score(ChartA, 990_000), Score(ChartB, 900_000));
        TheirBestsAre(Score(ChartA, 980_000), Score(ChartB, 950_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Shared);
        Assert.Equal(1, result.YouAhead);
        Assert.Equal(1, result.TheyAhead);
    }

    /// <summary>The shared rows lead, deficit first; then theirs alone; then yours alone.</summary>
    [Fact]
    public async Task SharedRowsLeadAndOneSidedRowsTrail()
    {
        var chartC = Guid.NewGuid();
        var chartD = Guid.NewGuid();
        MyBestsAre(Score(ChartA, 990_000), Score(ChartB, 900_000), Score(chartD, 970_000));
        TheirBestsAre(Score(ChartA, 980_000), Score(ChartB, 950_000), Score(chartC, 960_000));

        var result = await Compare();

        Assert.Equal(new[] { ChartB, ChartA, chartC, chartD }, result!.Rows.Select(r => r.ChartId));
    }

    /// <summary>A rival with nothing you both hold is a table of one-sided rows, not a crash and not a loss.</summary>
    [Fact]
    public async Task NothingComparableYieldsNoSharedRows()
    {
        MyBestsAre(Score(ChartA, null), Score(ChartB, null));
        TheirBestsAre(Score(ChartA, 950_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Equal(0, result!.Shared);
        Assert.Equal(1, result.OnlyThem);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task ThePlayerKeyedReadComparesAPublicPlayerWithoutAnEdge()
    {
        MyBestsAre(Score(ChartA, 990_000));
        TheirBestsAre(Score(ChartA, 980_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Equal("RIVAL", result!.Subject.DisplayName);
        Assert.Equal(_rival, result.Subject.UserId);
        Assert.Equal(1, result.YouAhead);
    }

    [Fact]
    public async Task ThePlayerKeyedReadIsNullForAPrivateStranger()
    {
        _users.Setup(u => u.GetUser(_rival, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(_rival).WithIsPublic(false).Build());

        Assert.Null(await Compare());
        _scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ThePlayerKeyedReadSeesAPrivatePlayerYouShareACommunityWith()
    {
        _users.Setup(u => u.GetUser(_rival, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(_rival).WithIsPublic(false).Build());
        _communities.Setup(c => c.GetUserCommunityMembers(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Name, IReadOnlyList<Guid>> { [Name.From("Crew")] = new[] { _me, _rival } });
        MyBestsAre(Score(ChartA, 990_000));
        TheirBestsAre(Score(ChartA, 999_000));

        var result = await Compare();

        Assert.NotNull(result);
        Assert.Equal(1, result!.TheyAhead);
    }

    [Fact]
    public async Task YouAreNotYourOwnOpponent()
    {
        Assert.Null(await Saga().Handle(new GetPlayerHeadToHeadQuery(MixEnum.Phoenix, _me), CancellationToken.None));
    }

    /// <summary>
    ///     The picker runs on Identity's visible-player search, so a private player you share a
    ///     community with is offered — the pool the player page opens for is the pool you can add
    ///     from. You are never a candidate; someone already on the roster is flagged, not hidden.
    /// </summary>
    [Fact]
    public async Task ThePickerOffersTheVisiblePoolMinusYourself()
    {
        var mate = Guid.NewGuid();
        var visible = new PlayerVisibility(true, false, false, false, new[] { Name.From("Crew") });
        _mediator.Setup(m => m.Send(It.Is<ScoreTracker.Identity.Contracts.Queries.SearchPlayersQuery>(q => q.Term == "ro"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScoreTracker.Identity.Contracts.PlayerSearchHit[]
            {
                new(_me, Name.From("Robby"), null, new Uri("https://x/me.png"), null,
                    new PlayerVisibility(true, true, true, false, Array.Empty<Name>())),
                new(mate, Name.From("Roxy"), null, new Uri("https://x/roxy.png"), null, visible),
                new(_rival, Name.From("RIVAL"), null, new Uri("https://x/rival.png"), null,
                    new PlayerVisibility(true, false, true, true, Array.Empty<Name>()))
            });
        _rivals.Setup(r => r.GetRivalsOwnedBy(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(_edgeId, _me, _rival, null, Added) });

        var candidates = await Saga().Handle(new SearchRivalCandidatesQuery("ro"), CancellationToken.None);

        Assert.DoesNotContain(candidates, c => c.UserId == _me);
        var roxy = candidates.Single(c => c.UserId == mate);
        Assert.False(roxy.IsPublic);
        Assert.True(roxy.SharesCommunity);
        Assert.False(roxy.AlreadyRival);
        Assert.True(candidates.Single(c => c.UserId == _rival).AlreadyRival);
    }
}
