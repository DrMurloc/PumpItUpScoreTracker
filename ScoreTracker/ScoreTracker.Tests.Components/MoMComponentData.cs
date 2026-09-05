using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A small real-shaped March of Murlocs session for the component tests: three Doubles charts
///     on a 1:45 window, three sessions on the board, one past season for the owner.
/// </summary>
internal static class MoMComponentData
{
    public static readonly Guid SessionId = Guid.NewGuid();
    public static readonly Guid RivalSessionId = Guid.NewGuid();
    public static readonly Guid ThirdSessionId = Guid.NewGuid();
    public static readonly Guid PastSessionId = Guid.NewGuid();
    public static readonly Guid SeasonId = Guid.NewGuid();
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(105);
    public static readonly User Kim = Player("김재현");
    public static readonly User Yimmy = Player("yimmythe42");
    public static readonly User Third = Player("tieny");

    public static User Player(string name) =>
        new(Guid.NewGuid(), Name.From(name), true, null, new Uri("https://example.invalid/p.png"), null);

    public static Chart Chart(string name, int level, int seconds) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From(name), SongType.Arcade, new Uri($"https://example.invalid/{name.Replace(' ', '-')}.png"),
                TimeSpan.FromSeconds(seconds), Name.From("Artist"), null),
            ChartType.Double, DifficultyLevel.From(level), MixEnum.Phoenix, null, null);

    public static MoMSessionChart Row(Chart chart, int score, int points, double balanced, int bonus = 0, DateTimeOffset? playedAt = null) =>
        new(chart, PhoenixScore.From(score), PhoenixPlate.FairGame, false, points, bonus, balanced, playedAt);

    public static IReadOnlyList<MoMTimedChart> Timeline(params MoMSessionChart[] rows)
    {
        var songTime = TimeSpan.FromTicks(rows.Sum(r => r.Chart.Song.Duration.Ticks));
        var gap = rows.Length > 1 ? (Window - songTime) / (rows.Length - 1) : TimeSpan.Zero;
        var cursor = TimeSpan.Zero;
        var result = new List<MoMTimedChart>();
        foreach (var r in rows)
        {
            result.Add(new MoMTimedChart(r, cursor, r.Chart.Song.Duration, r.SessionScore / r.Chart.Song.Duration.TotalSeconds));
            cursor += r.Chart.Song.Duration + gap;
        }

        return result;
    }

    public static MoMLevers Levers(int charts, double balanced, double folder, int avgScore, TimeSpan downtime, TimeSpan songTime, int total) =>
        new(charts, balanced, folder, PhoenixScore.From(avgScore), PhoenixScore.From(avgScore).LetterGradeFor(MixEnum.Phoenix), downtime, songTime, total);

    public static MoMSessionView Session(bool draft = false)
    {
        var slam = Row(Chart("Slam", 24, 99), 976489, 1528, 24.5);
        var gargoyle = Row(Chart("Gargoyle - FULL SONG -", 25, 378), 844710, 3207, 25.5);
        var adrenaline = Row(Chart("Adrenaline Blaster", 23, 119), 976959, 1653, 23.98, bonus: 176);
        var timeline = Timeline(slam, adrenaline, gargoyle);
        var mine = Levers(3, 24.66, 24.0, 932719, Window - TimeSpan.FromSeconds(596), TimeSpan.FromSeconds(596), 6388);
        var rival = Levers(2, 24.5, 24.0, 950000, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(75), 5000);
        var third = Levers(4, 23.5, 23.0, 900000, TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(65), 4000);
        var season = new MoMSeasonSummary(SeasonId, "Winter 2025", new DateTimeOffset(2025, 2, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero), true);
        var board = new[]
        {
            new MoMBoardSessionSummary(SessionId, Kim.Id, Kim, 1, 1, mine),
            new MoMBoardSessionSummary(RivalSessionId, Yimmy.Id, Yimmy, 2, 1, rival),
            new MoMBoardSessionSummary(ThirdSessionId, Third.Id, Third, 3, 1, third)
        };
        return new MoMSessionView(SessionId, season, Guid.NewGuid(), ChartType.Double, MixEnum.Phoenix, Window, Kim.Id, Kim,
            draft ? null : new DateTimeOffset(2025, 2, 14, 0, 0, 0, TimeSpan.Zero), new Uri("https://youtu.be/x"), 6388,
            draft ? 0 : 1, 3, mine, new MoMLeverPlaces(2, 1, 2, 1, 3), timeline, board,
            new[] { new MoMPastSession(PastSessionId, new MoMSeasonSummary(Guid.NewGuid(), "March of Murlocs 2", season.StartsAt.AddMonths(-8), season.StartsAt.AddMonths(-6), false), 4400, season.StartsAt.AddMonths(-7)) });
    }
}
