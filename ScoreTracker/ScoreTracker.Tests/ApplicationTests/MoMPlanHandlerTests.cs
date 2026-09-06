using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Planner's query (docs/design/march-of-murlocs.md §11.5): a record book priced under the
///     board's own frozen configuration, the solver's set inside it, and the four numbers. The
///     solver itself is <see cref="ScoreTracker.Tests.DomainTests.MoMPlannerTests" />.
/// </summary>
public sealed class MoMPlanHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly MoMReadHandlerFixture _fixture = new();
    private readonly List<RecordedPhoenixScore> _bests = new();
    private readonly Dictionary<Guid, PhoenixScore> _projected = new();
    private readonly List<RestChartFacts> _rest = new();
    private User _me = null!;
    private bool _loggedIn = true;

    private MoMPlanHandler Handler()
    {
        var read = new Mock<IMoMReadRepository>();
        read.Setup(m => m.GetSeasons(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fixture.Seasons.ToArray());
        read.Setup(m => m.GetBoard(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _fixture.Boards.FirstOrDefault(b => b.Id == id));
        read.Setup(m => m.GetBoards(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Boards.Where(b => ids.Contains(b.SeasonId)).ToArray());
        read.Setup(m => m.GetPublishedSessions(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Sessions.Where(s => s.PublishedAt != null && ids.Contains(s.BoardId)).ToArray());

        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                _fixture.Charts.Where(c => ids == null || ids.Contains(c.Id)).ToArray());

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _bests.ToArray());

        var projector = new Mock<IScoreProjector>();
        projector.Setup(p => p.Project(It.IsAny<ScoreProjectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ScoreProjection(_projected, _projected.Count, 24, 1));

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRestChartFactsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IReadOnlyList<RestChartFacts>)_rest.ToArray());

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(() => _loggedIn);
        currentUser.SetupGet(c => c.User).Returns(() => _me);

        return new MoMPlanHandler(read.Object, charts.Object, scores.Object, projector.Object,
            currentUser.Object, mediator.Object);
    }

    private MoMBoardInfo Board()
    {
        var season = _fixture.Season("Summer 2026", Now.AddMonths(-1), Now.AddMonths(2));
        _me = _fixture.User("DRMURLOC");
        return _fixture.Board(season, ChartType.Double);
    }

    /// <summary>A held record on a chart of the given level and length.</summary>
    private Chart Held(string name, int level, int seconds, int score)
    {
        var chart = _fixture.Chart(name, level, seconds);
        _bests.Add(new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.MarvelousGame, false, Now));
        return chart;
    }

    private Task<MoMPlanView?> Plan(MoMBoardInfo board, MoMEnergy energy = MoMEnergy.TopOfMyGame,
        MoMPush push = MoMPush.AllOut, int rest = 35) =>
        Handler().Handle(new BuildMoMPlanQuery(board.Id, energy, push, rest), CancellationToken.None);

    [Fact]
    public async Task ASignedOutVisitorHasNoRecordBookToPlanFrom()
    {
        var board = Board();
        _loggedIn = false;

        Assert.Null(await Plan(board));
    }

    [Fact]
    public async Task ABoardThatDoesNotExistPlansNothing()
    {
        Board();

        Assert.Null(await Handler().Handle(new BuildMoMPlanQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task TheBookIsPricedUnderTheBoardsOwnConfigurationAndTheSetIsMarkedInIt()
    {
        var board = Board();
        Held("Slam", 24, 128, 980000);
        Held("Gargoyle", 20, 115, 986121);

        var plan = await Plan(board);

        Assert.Equal(2, plan!.Charts.Count);
        Assert.All(plan.Charts, c => Assert.True(c.Points > 0));
        Assert.All(plan.Charts, c => Assert.False(c.IsProjected));
        Assert.Equal(2, plan.ChartsPlanned);
        Assert.Equal(plan.Charts.Sum(c => c.Points), plan.ProjectedPoints);
        Assert.Single(plan.Charts, c => c.IsClosing);
        // The set leads the list in the order it would be played.
        Assert.True(plan.Charts[0].InSet);
    }

    [Fact]
    public async Task AChartYouHaveNeverPlayedIsAbsentAtTheTopRungAndPricedAtTheOthers()
    {
        var board = Board();
        Held("Slam", 24, 128, 980000);
        var unplayed = _fixture.Chart("Never played", 22, 120);

        var top = await Plan(board);
        Assert.DoesNotContain(top!.Charts, c => c.Chart.Id == unplayed.Id);

        _projected[unplayed.Id] = 950000;
        var great = await Plan(board, MoMEnergy.Great);

        var projected = Assert.Single(great!.Charts, c => c.Chart.Id == unplayed.Id);
        Assert.True(projected.IsProjected);
        Assert.Equal(950000, (int)projected.Score);
    }

    [Fact]
    public async Task ThePeerRungsFallBackToYourOwnRecordWhereThePeersHaveNoOpinion()
    {
        var board = Board();
        var slam = Held("Slam", 24, 128, 980000);

        var plan = await Plan(board, MoMEnergy.Great);

        var row = Assert.Single(plan!.Charts, c => c.Chart.Id == slam.Id);
        Assert.False(row.IsProjected);
        Assert.Equal(980000, (int)row.Score);
    }

    [Fact]
    public async Task ThePushCapHangsOffYourLastSessionsAverage()
    {
        var board = Board();
        Held("Easy", 22, 120, 980000);
        Held("Hard", 25, 120, 980000);
        // A published session averaging D23.67, so Steady caps at 22 and Push at 23.
        _fixture.Session(board, _me, 59319, Now.AddDays(-3), averageDifficulty: 23.67);

        var steady = await Plan(board, push: MoMPush.Steady);
        var push = await Plan(board, push: MoMPush.Push);
        var allOut = await Plan(board, push: MoMPush.AllOut);

        Assert.Equal(22, steady!.LevelCap);
        Assert.Equal(23, push!.LevelCap);
        Assert.Null(allOut!.LevelCap);
        Assert.Equal(23.67, steady.Anchor);
        Assert.Equal(1, steady.ChartsPlanned);
        Assert.Equal(2, allOut.ChartsPlanned);
    }

    [Fact]
    public async Task WithNoSessionYetTheAnchorIsTheLevelYouHoldMostOf()
    {
        var board = Board();
        Held("A", 21, 120, 980000);
        Held("B", 21, 120, 980000);
        Held("C", 25, 120, 980000);

        var plan = await Plan(board, push: MoMPush.Push);

        Assert.Equal(21.5, plan!.Anchor);
        Assert.Equal(21, plan.LevelCap);
    }

    [Fact]
    public async Task TheConversionIsWhatYouBankedAgainstWhatTheBookPlans()
    {
        var board = Board();
        Held("Slam", 24, 128, 980000);
        _fixture.Session(board, _me, 700, Now.AddDays(-3));

        var plan = await Plan(board);

        Assert.Equal(700, plan!.BankedThisSeason);
        Assert.Equal(700.0 / plan.ProjectedPoints, plan.Conversion!.Value, 6);
    }

    [Fact]
    public async Task RestChartFactsRideAlongWithTheChartsTheyDescribe()
    {
        var board = Board();
        var slam = Held("Slam", 24, 128, 980000);
        _rest.Add(new RestChartFacts(slam.Id, true, 4.7, 6, true, 0.53, 79, true, true, 0.0, true, 2.9, 7, true));

        var plan = await Plan(board);

        var row = Assert.Single(plan!.Charts);
        Assert.NotNull(row.Rest);
        Assert.True(row.Rest!.IsRest);
        Assert.True(row.IsFinisher == (row.Chart.Song.Duration >= TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public async Task AnEmptyRecordBookPlansNothingWithoutFallingOver()
    {
        var board = Board();

        var plan = await Plan(board);

        Assert.Empty(plan!.Charts);
        Assert.Equal(0, plan.ChartsPlanned);
        Assert.Equal(0, plan.ProjectedPoints);
        Assert.Null(plan.Conversion);
        Assert.Equal(board.Configuration.MaxTime, plan.Downtime);
    }
}
