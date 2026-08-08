using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class SessionBreakdownBuilderTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AChartPlayedSeveralTimesInOneSessionBuildsOneBoard()
    {
        // The shape a session with attempts always has: six losing plays and the clear that
        // ended them, all on one chart. Treating the row list as one-per-chart threw here.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = Enumerable.Range(0, 6)
            .Select(i => Row(chart.Id, Start.AddMinutes(i * 7), 400000 + i * 60000, broken: true,
                ScoreEventClassification.Played))
            .Append(Row(chart.Id, Start.AddMinutes(45), 912400, false, ScoreEventClassification.NewPass))
            .ToArray();

        var model = await Build(chart, rows, peers: new[] { Peer("MIDNIGHT", 20.9, 930000) });

        Assert.NotNull(model.Hero);
        Assert.Equal(7, model.Hero!.Scores.Count);
        Assert.Single(model.Hero.PeerBoards);
    }

    /// <summary>
    ///     Six clubmates against a board that shows five, so the cap actually bites. FAR holds
    ///     the top score and sits nearly three levels off; everyone else is within reach.
    /// </summary>
    private static CommunityPeerScore[] SixClubmates()
    {
        return new[]
        {
            Peer("FAR", 19.8, 998000),
            Peer("A", 22.5, 901000),
            Peer("B", 22.7, 950000),
            Peer("C", 22.4, 930000),
            Peer("D", 22.8, 910000),
            Peer("E", 22.3, 940000)
        };
    }

    [Fact]
    public async Task ClosenessPicksWhoAppearsAndScoreOrdersThem()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, SixClubmates());

        var board = Assert.Single(model.Hero!.PeerBoards);
        // FAR is dropped for distance despite the best score; the rest read high to low.
        Assert.Equal(new[] { "B", "E", "C", "D", "A" },
            board.Peers.Select(p => p.Score.PlayerName.ToString()));
    }

    [Fact]
    public async Task PeerPlacesAreRealStandingsAcrossTheWholeClub()
    {
        // FAR is #1 on the chart and is not shown, so the five that are keep places 2..6 rather
        // than being renumbered from one. A place counting only the visible rows would be a
        // different claim entirely.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, SixClubmates());

        var board = Assert.Single(model.Hero!.PeerBoards);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, board.Peers.Select(p => p.Place));
    }

    [Fact]
    public async Task PagingTheHistoryLeavesTheHeroExactlyWhereItWas()
    {
        // The hero is not what you paged. Rebuilding it is both wasted work and the reason the
        // interaction used to look like a navigation.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows, Array.Empty<CommunityPeerScore>());

        var paged = await builder.Refilter(model, User, 2, 20, null, CancellationToken.None);

        Assert.Same(model.Hero, paged.Hero);
    }

    [Fact]
    public async Task PromotingACardLeavesTheHistoryExactlyWhereItWas()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows, Array.Empty<CommunityPeerScore>());

        var reselected = await builder.Reselect(model, User, Session, 1, 20, null, CancellationToken.None);

        Assert.Same(model.History, reselected.History);
        Assert.NotNull(reselected.Hero);
    }

    [Fact]
    public async Task AFreshSessionWithNothingCapturedYetIsPending()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(),
            captured: false, sessionEndedMinutesAgo: 0);

        Assert.True(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task ASessionOlderThanTheWindowStopsClaimingToBeCalculating()
    {
        // A session that genuinely earned nothing looks identical to one still being worked out.
        // The window is what stops the page telling that player to keep waiting forever.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(),
            captured: false, sessionEndedMinutesAgo: 30);

        Assert.False(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task ASessionWithCapturedRowsIsNeverPending()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(),
            captured: true, sessionEndedMinutesAgo: 0);

        Assert.False(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task ASessionPredatingTheSessionTableIsNeverPending()
    {
        // No ScoreSession row means no wall clock to test against — and those sessions are
        // historical by definition, so "still calculating" could never be true of them.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), captured: false);

        Assert.False(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task APhoenix2ScorePastYourPhoenix1BestReportsHowFarPast()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Equal(20000, model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task AnEarlierPhoenix2ScoreAlreadyPastPhoenix1SpendsTheMark()
    {
        // The mark is the moment you passed your old self, so it must not ride every later
        // upscore on a chart you already took.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { RowFrom(chart.Id, 975000, previousBest: 950000) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task APhoenixSessionNeverComparesAgainstPhoenix1()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), MixEnum.Phoenix,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task ABrokenPhoenix1RecordIsNotABestToHavePassed()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000, broken: true) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task MatchingYourPhoenix1BestIsNotPassingIt()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 940000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, Array.Empty<CommunityPeerScore>(), MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    private static async Task<SessionsPageModel> Build(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows, CommunityPeerScore[] peers,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null)
    {
        return (await BuildWith(chart, rows, peers, mix, phoenix1, captured, sessionEndedMinutesAgo)).Model;
    }

    private static async Task<(SessionBreakdownBuilder Builder, SessionsPageModel Model)> BuildWith(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows, CommunityPeerScore[] peers,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null)
    {
        var mediator = new Mock<IMediator>();
        var group = new RecentSessionsPage.SessionGroup(Session, null, mix, "officialImport",
            rows.Min(r => r.OccurredAt), rows.Max(r => r.OccurredAt), rows);

        // Wall clock, deliberately distinct from the journal's play date: "is capture still
        // running" is a question about when the scores reached us.
        var now = Start.AddHours(4);
        var sessions = sessionEndedMinutesAgo == null
            ? Array.Empty<ScoreSessionRecord>()
            : new[]
            {
                new ScoreSessionRecord(Session, User, mix, "officialImport", "SHIRONEKO", "2",
                    now.AddMinutes(-sessionEndedMinutesAgo.Value - 5),
                    now.AddMinutes(-sessionEndedMinutesAgo.Value), rows.Length, 1, 0)
            };

        Setup(mediator, new GetRecentSessionsQuery(User, 1, 20),
            new RecentSessionsPage(1, new[] { group }));
        Setup(mediator, new GetScoreSessionsQuery(User), (IReadOnlyList<ScoreSessionRecord>)sessions);
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        mediator.Setup(m => m.Send(It.IsAny<GetScoreHighlightsForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(captured
                ? new[]
                {
                    new ScoreHighlightRecord(chart.Id, Session, Start, HighlightFlags.FolderDebut, 21, 21.0,
                        new HighlightDetail(PeerPercentile: 0.4, AttemptsBeforeClear: 6))
                }
                : Array.Empty<ScoreHighlightRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerMilestonesForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerMilestoneRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(User, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                22.6, 22.6, 23.4));
        mediator.Setup(m => m.Send(It.IsAny<GetCommunityPeerScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IReadOnlyList<CommunityPeerScore>>)
                new Dictionary<Guid, IReadOnlyList<CommunityPeerScore>> { [chart.Id] = peers });

        var readers = new Mock<IUserReader>();
        readers.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        var ledger = new Mock<IScoreReader>();
        ledger.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenix1 ?? Array.Empty<UserPhoenixScore>());
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(now);
        var builder = new SessionBreakdownBuilder(mediator.Object, readers.Object, ledger.Object, clock.Object);
        return (builder, await builder.Build(User, null, 1, 20, null, CancellationToken.None));
    }

    private static void Setup<T>(Mock<IMediator> mediator, IRequest<T> request, T result)
    {
        mediator.Setup(m => m.Send(It.Is<IRequest<T>>(r => r.GetType() == request.GetType()),
            It.IsAny<CancellationToken>())).ReturnsAsync(result);
    }

    private static Chart ChartAt(ChartType type, int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }

    private static RecentSessionsPage.ScoreEventRecord Row(Guid chartId, DateTimeOffset at, int score,
        bool broken, ScoreEventClassification classification)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, at, score, broken ? null : "Fair Game",
            broken, "seed", Session, classification, null);
    }

    private static RecentSessionsPage.ScoreEventRecord RowFrom(Guid chartId, int score, int previousBest)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, Start, score, "Fair Game", false, "seed",
            Session, ScoreEventClassification.Upscore, previousBest);
    }

    private static UserPhoenixScore Phoenix1(Guid chartId, int score, bool broken = false)
    {
        return new UserPhoenixScore(User, chartId, Name.From("DrMurloc"), PhoenixScore.From(score),
            PhoenixPlate.FairGame, broken);
    }

    private static CommunityPeerScore Peer(string name, double competitive, int score)
    {
        return new CommunityPeerScore(Guid.NewGuid(), Name.From(name), new[] { Name.From("Arrow Eclipse") },
            competitive, PhoenixScore.From(score), PhoenixPlate.FairGame, false);
    }
}
