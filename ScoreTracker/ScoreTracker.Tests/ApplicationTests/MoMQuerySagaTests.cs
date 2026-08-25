using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MoMQuerySagaTests
{
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly MoMSeason Spring = Season("Spring 2026", 2026, 2,
        new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.Zero));

    private static readonly MoMSeason Summer = Season("Summer 2026", 2026, 3,
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero));

    [Fact]
    public async Task NoSelectorResolvesTheLiveSeasonWithItsNeighbours()
    {
        var context = new Context().WithSeasons(Spring, Summer);
        var doubles = context.AddBoard(Summer, ChartType.Double);
        context.AddPublishedSession(doubles, Guid.NewGuid(), 5000, Now.AddDays(-2));

        var view = await context.Saga.Handle(new GetMoMSeasonQuery(), CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(Summer.Id, view!.Id);
        Assert.True(view.IsLive);
        Assert.Equal(Spring.Id, view.Previous?.Id);
        Assert.Null(view.Next);
        var board = Assert.Single(view.Boards);
        Assert.Equal(1, board.SessionCount);
    }

    [Fact]
    public async Task BetweenSeasonsTheMostRecentStartedSeasonAnswers()
    {
        // Both seasons ended and the next cycle tick has not fired: the page still renders.
        var context = new Context(now: Summer.EndsAt.AddDays(3)).WithSeasons(Spring, Summer);

        var view = await context.Saga.Handle(new GetMoMSeasonQuery(), CancellationToken.None);

        Assert.Equal(Summer.Id, view!.Id);
        Assert.False(view.IsLive);
    }

    [Fact]
    public async Task YearAndQuarterResolveAQuarterlySeason()
    {
        var context = new Context().WithSeasons(Spring, Summer);

        var view = await context.Saga.Handle(new GetMoMSeasonQuery(2026, 2), CancellationToken.None);

        Assert.Equal(Spring.Id, view!.Id);
        Assert.Equal(Summer.Id, view.Next?.Id);
    }

    [Fact]
    public async Task LegacyNameResolvesWithHyphensAsSpacesCaseInsensitively()
    {
        var legacy = Season("March of Murlocs 2", null, null,
            new DateTimeOffset(2024, 6, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 8, 8, 0, 0, 0, TimeSpan.Zero));
        var context = new Context().WithSeasons(legacy, Summer);

        var view = await context.Saga.Handle(
            new GetMoMSeasonQuery(LegacyName: "march-of-murlocs-2"), CancellationToken.None);

        Assert.Equal(legacy.Id, view!.Id);
    }

    [Fact]
    public async Task BoardRanksByScoreThenEarliestPublication()
    {
        var context = new Context().WithSeasons(Summer);
        var board = context.AddBoard(Summer, ChartType.Double);
        var alice = context.AddUser("Alice");
        var bob = context.AddUser("Bob");
        var carol = context.AddUser("Carol");
        context.AddPublishedSession(board, alice, 5000, Now.AddDays(-1));
        context.AddPublishedSession(board, bob, 6000, Now.AddDays(-2));
        // Same score as Alice but published earlier — the earlier publication wins the tie.
        context.AddPublishedSession(board, carol, 5000, Now.AddDays(-3));

        var view = await context.Saga.Handle(new GetMoMBoardQuery(board), CancellationToken.None);

        Assert.Equal(new[] { "Bob", "Carol", "Alice" },
            view!.Rows.Select(r => r.UserName).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, view.Rows.Select(r => r.Place).ToArray());
    }

    [Fact]
    public async Task DraftIsInvisibleToAnyoneButItsOwnerAndTheAdmin()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var context = new Context().WithSeasons(Summer);
        var board = context.AddBoard(Summer, ChartType.Double);
        context.AddUser("Owner", owner);
        var draft = context.AddDraft(board, owner);

        context.LogInAs(stranger);
        Assert.Null(await context.Saga.Handle(new GetMoMSessionQuery(draft), CancellationToken.None));

        context.LogOut();
        Assert.Null(await context.Saga.Handle(new GetMoMSessionQuery(draft), CancellationToken.None));

        context.LogInAs(owner);
        var ownView = await context.Saga.Handle(new GetMoMSessionQuery(draft), CancellationToken.None);
        Assert.NotNull(ownView);
        Assert.True(ownView!.IsDraft);
        Assert.Null(ownView.Place);

        context.LogInAs(AdminId);
        Assert.NotNull(await context.Saga.Handle(new GetMoMSessionQuery(draft), CancellationToken.None));
    }

    [Fact]
    public async Task SessionViewComputesPlaceAndBalancedLevels()
    {
        var me = Guid.NewGuid();
        var context = new Context().WithSeasons(Summer);
        var board = context.AddBoard(Summer, ChartType.Double);
        context.AddUser("Me", me);
        context.AddPublishedSession(board, Guid.NewGuid(), 7000, Now.AddDays(-3));
        var mine = context.AddPublishedSession(board, me, 6000, Now.AddDays(-2));
        context.AddPublishedSession(board, Guid.NewGuid(), 5000, Now.AddDays(-1));

        // One chart the season re-rated (snapshot 21.5) and one it left alone (15 + 0.5).
        var rerated = context.AddChart(20, ChartType.Double);
        var untouched = context.AddChart(15, ChartType.Double);
        context.SetSnapshot(board, (rerated.Id, 21.5));
        context.SetSessionCharts(mine,
            new MoMSessionChartRecord(0, rerated.Id, 990000, "SuperbGame", false, 1500, 25, Now.AddDays(-2)),
            new MoMSessionChartRecord(1, untouched.Id, 960000, "FairGame", true, 800, 0, null));

        var view = await context.Saga.Handle(new GetMoMSessionQuery(mine), CancellationToken.None);

        Assert.Equal(2, view!.Place);
        Assert.Equal(TimeSpan.FromMinutes(105), view.MaxTime);
        Assert.Equal(21.5, view.Charts[0].BalancedLevel);
        Assert.Equal(PhoenixPlate.SuperbGame, view.Charts[0].Plate);
        Assert.Equal(15.5, view.Charts[1].BalancedLevel);
        Assert.True(view.Charts[1].IsBroken);
        Assert.Equal(Now.AddDays(-2), view.Charts[0].PlayedAt);
    }

    [Fact]
    public async Task SeasonsListingReportsWinnerAndTheViewersBestStanding()
    {
        var me = Guid.NewGuid();
        var context = new Context().WithSeasons(Spring, Summer);
        var played = context.AddBoard(Spring, ChartType.Double);
        var empty = context.AddBoard(Summer, ChartType.Single);
        var bob = context.AddUser("Bob");
        context.AddUser("Me", me);
        context.AddPublishedSession(played, bob, 6000, Now.AddDays(-100));
        var myBest = context.AddPublishedSession(played, me, 5000, Now.AddDays(-99));
        context.AddPublishedSession(played, me, 4000, Now.AddDays(-98));
        context.LogInAs(me);

        var listing = await context.Saga.Handle(new GetMoMSeasonsQuery(), CancellationToken.None);

        // Newest first: the live Summer season leads.
        Assert.Equal(Summer.Id, listing[0].Season.Id);
        var emptyBoard = Assert.Single(listing[0].Boards);
        Assert.Equal(0, emptyBoard.SessionCount);
        Assert.Null(emptyBoard.WinnerName);
        Assert.Null(emptyBoard.YourPlace);

        var playedBoard = Assert.Single(listing[1].Boards);
        Assert.Equal(3, playedBoard.SessionCount);
        Assert.Equal("Bob", playedBoard.WinnerName);
        Assert.Equal(6000, playedBoard.WinnerScore);
        Assert.Equal(2, playedBoard.YourPlace);
        Assert.Equal(5000, playedBoard.YourScore);
        Assert.Equal(myBest, playedBoard.YourBestSessionId);
    }

    private static MoMSeason Season(string name, int? year, byte? quarter,
        DateTimeOffset start, DateTimeOffset end)
    {
        return new MoMSeason(Guid.NewGuid(), year, quarter, name, start, end, start);
    }

    /// <summary>
    ///     Stub-backed world for the saga: seasons, boards, sessions and users registered
    ///     here answer through the mocked ports exactly as storage would.
    /// </summary>
    private sealed class Context
    {
        private readonly List<MoMBoardRecord> _boards = new();
        private readonly Mock<IChartRepository> _charts = new();
        private readonly List<Chart> _chartList = new();
        private readonly Mock<ICurrentUserAccessor> _currentUser = new();
        private readonly Mock<IMoMRepository> _mom = new();
        private readonly DateTimeOffset _now;
        private readonly List<MoMSessionRecord> _sessions = new();
        private readonly List<User> _users = new();

        public Context(DateTimeOffset? now = null)
        {
            _now = now ?? Now;
            LogOut();
            _mom.Setup(m => m.GetBoards(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _boards.ToArray());
            _mom.Setup(m => m.GetPublishedSessions(It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> boardIds, CancellationToken _) =>
                    _sessions.Where(s => s.PublishedAt != null && boardIds.Contains(s.BoardId))
                        .ToArray());
            _mom.Setup(m => m.GetSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) =>
                    _sessions.FirstOrDefault(s => s.Id == id));
            _mom.Setup(m => m.GetSessionCharts(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<MoMSessionChartRecord>());
            _mom.Setup(m => m.GetSeasonSnapshot(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            _mom.Setup(m => m.GetBoardConfiguration(It.IsAny<Guid>(), It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid boardId, bool _, CancellationToken _) =>
                    new TournamentConfiguration(boardId, "Board", new ScoringConfiguration(),
                        false, true)
                    {
                        MaxTime = TimeSpan.FromMinutes(105),
                        AllowRepeats = false
                    });
            var users = new Mock<IUserReader>();
            users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    _users.Where(u => ids.Contains(u.Id)).ToArray());
            users.Setup(u => u.GetUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) =>
                    _users.FirstOrDefault(u => u.Id == id));
            Users = users;
            _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null,
                    It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _,
                        IEnumerable<Guid>? ids, CancellationToken _) =>
                    ids == null ? _chartList.ToArray() : _chartList.Where(c => ids.Contains(c.Id)).ToArray());
        }

        private Mock<IUserReader> Users { get; }

        public MoMQuerySaga Saga => new(_mom.Object, Users.Object, _currentUser.Object,
            FakeDateTime.At(_now).Object, _charts.Object);

        public Context WithSeasons(params MoMSeason[] seasons)
        {
            _mom.Setup(m => m.GetSeasons(It.IsAny<CancellationToken>())).ReturnsAsync(seasons);
            return this;
        }

        public Guid AddBoard(MoMSeason season, ChartType type, MixEnum mix = MixEnum.Phoenix)
        {
            var id = Guid.NewGuid();
            _boards.Add(new MoMBoardRecord(id, season.Id, mix, type));
            return id;
        }

        public Guid AddUser(string name, Guid? id = null)
        {
            var userId = id ?? Guid.NewGuid();
            _users.Add(new UserBuilder().WithId(userId).WithName(name).Build());
            return userId;
        }

        public Guid AddPublishedSession(Guid boardId, Guid userId, int totalScore,
            DateTimeOffset publishedAt)
        {
            var id = Guid.NewGuid();
            _sessions.Add(new MoMSessionRecord(id, boardId, userId, publishedAt, totalScore,
                2, TimeSpan.FromMinutes(13).Ticks, 18.5, 11.5, 15, 20, null));
            return id;
        }

        public Guid AddDraft(Guid boardId, Guid userId)
        {
            var id = Guid.NewGuid();
            _sessions.Add(new MoMSessionRecord(id, boardId, userId, null, 0, 0, 0, 0, 0, 0, 0,
                null));
            return id;
        }

        public Chart AddChart(int level, ChartType type)
        {
            var chart = new ChartBuilder().WithLevel(level).WithType(type).Build();
            _chartList.Add(chart);
            return chart;
        }

        public void SetSnapshot(Guid boardId, params (Guid ChartId, double Level)[] deltas)
        {
            _mom.Setup(m => m.GetSeasonSnapshot(boardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deltas.ToDictionary(d => d.ChartId, d => d.Level));
        }

        public void SetSessionCharts(Guid sessionId, params MoMSessionChartRecord[] rows)
        {
            _mom.Setup(m => m.GetSessionCharts(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows);
        }

        public void LogInAs(Guid userId)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId)
                       ?? new UserBuilder().WithId(userId).WithName("Viewer").Build();
            _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
            _currentUser.Setup(c => c.User).Returns(user);
        }

        public void LogOut()
        {
            _currentUser.Setup(c => c.IsLoggedIn).Returns(false);
        }
    }
}
