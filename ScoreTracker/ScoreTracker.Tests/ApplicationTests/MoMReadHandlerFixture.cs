using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     A MoM read model in memory: seasons, boards with frozen configurations, stored sessions,
///     their chart rows, the catalog and the players — every port the read handler depends on,
///     stubbed to answer from these lists exactly as the repository would.
/// </summary>
internal sealed class MoMReadHandlerFixture
{
    public static readonly DateTimeOffset Now = new(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(105);

    public List<MoMSeason> Seasons { get; } = new();
    public List<MoMBoardInfo> Boards { get; } = new();
    public List<MoMStoredSession> Sessions { get; } = new();
    public List<MoMStoredSessionChart> Rows { get; } = new();
    public List<Chart> Charts { get; } = new();
    public List<User> Users { get; } = new();

    public MoMSeason Season(string name, DateTimeOffset start, DateTimeOffset end)
    {
        var season = new MoMSeason(Guid.NewGuid(), null, null, name, start, end, start);
        Seasons.Add(season);
        return season;
    }

    public MoMBoardInfo Board(MoMSeason season, ChartType type, MixEnum mix = MixEnum.Phoenix,
        Action<ScoringConfiguration>? tune = null)
    {
        var scoring = ScoringConfiguration.PumbilityPlus;
        scoring.AdjustToTime = true;
        tune?.Invoke(scoring);
        var id = Guid.NewGuid();
        var board = new MoMBoardInfo(id, season.Id, mix, type,
            new TournamentConfiguration(id, Name.From("board"), scoring, true, true) { MaxTime = Window });
        Boards.Add(board);
        return board;
    }

    public MoMStoredSession Session(MoMBoardInfo board, User user, int total, DateTimeOffset? published,
        int charts = 30, double averageDifficulty = 23.5, TimeSpan? downtime = null)
    {
        var session = new MoMStoredSession(Guid.NewGuid(), board.Id, user.Id, published, total, charts,
            downtime ?? TimeSpan.FromMinutes(20), averageDifficulty, 8, 20, 26, null, published ?? Now);
        Sessions.Add(session);
        return session;
    }

    public Chart Chart(string name, int level, int seconds = 120, ChartType type = ChartType.Double)
    {
        var chart = MoMRealSessions.Chart(name, level, seconds, type);
        if (Charts.All(c => c.Id != chart.Id)) Charts.Add(chart);
        return chart;
    }

    public MoMStoredSessionChart Row(MoMStoredSession session, Chart chart, int score, int points, int ordinal = 0)
    {
        var row = new MoMStoredSessionChart(session.Id, ordinal, chart.Id, score, PhoenixPlate.RoughGame, false,
            points, 0, null);
        Rows.Add(row);
        return row;
    }

    public User User(string name)
    {
        var user = new User(Guid.NewGuid(), Name.From(name), true, null, new Uri("https://example.invalid/a.png"), null);
        Users.Add(user);
        return user;
    }

    public MoMReadHandler Handler()
    {
        var mom = new Mock<IMoMReadRepository>();
        mom.Setup(m => m.GetSeasons(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Seasons.OrderByDescending(s => s.StartsAt).ToArray());
        mom.Setup(m => m.GetBoards(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                Boards.Where(b => ids.Contains(b.SeasonId)).ToArray());
        mom.Setup(m => m.GetBoard(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Boards.FirstOrDefault(b => b.Id == id));
        mom.Setup(m => m.GetPublishedSessions(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                Sessions.Where(s => s.PublishedAt != null && ids.Contains(s.BoardId)).ToArray());
        mom.Setup(m => m.GetSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Sessions.FirstOrDefault(s => s.Id == id));
        mom.Setup(m => m.GetSessionCharts(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                Rows.Where(r => ids.Contains(r.SessionId)).ToArray());
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                Charts.Where(c => ids == null || ids.Contains(c.Id)).ToArray());
        var users = new Mock<IUserReader>();
        users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => Users.Where(u => ids.Contains(u.Id)).ToArray());
        return new MoMReadHandler(mom.Object, charts.Object, users.Object, FakeDateTime.At(Now).Object);
    }
}
