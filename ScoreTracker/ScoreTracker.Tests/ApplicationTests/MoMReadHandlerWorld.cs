using System;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     A small board with real shapes: three published Doubles sessions on Winter 2025 (whose
///     level-24 rating is 300 higher than the older season's), 김재현's older Doubles session on
///     March of Murlocs 2, and a Singles session of his that must never be compared with them.
/// </summary>
internal sealed record MoMReadHandlerWorld(MoMReadHandlerFixture F, MoMBoardInfo Doubles, MoMBoardInfo OldDoubles,
    MoMStoredSession Kim, MoMStoredSession Tieny, MoMStoredSession Third, MoMStoredSession KimBefore,
    Guid KimId, Guid Slam, Guid Odin)
{
    public static readonly DateTimeOffset Feb = new(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);

    public static MoMReadHandlerWorld Build()
    {
        var f = new MoMReadHandlerFixture();
        var mom2 = f.Season("March of Murlocs 2", Feb.AddMonths(-8), Feb.AddMonths(-6));
        var winter = f.Season("Winter 2025", Feb, Feb.AddMonths(2));
        var doubles = f.Board(winter, ChartType.Double, tune: s => s.LevelRatings[DifficultyLevel.From(24)] += 300);
        var singles = f.Board(winter, ChartType.Single);
        var oldDoubles = f.Board(mom2, ChartType.Double);
        var kim = f.User("김재현");
        var tieny = f.User("tieny");
        var third = f.User("third");
        var slam = f.Chart("Slam", 24, 99);
        var odin = f.Chart("Odin", 23, 127);
        var gargoyle = f.Chart("Gargoyle - FULL SONG -", 25, 378);
        var k = f.Session(doubles, kim, 3000, Feb.AddDays(13));
        f.Row(k, slam, 976489, 1600, 0);
        f.Row(k, odin, 976240, 1400, 1);
        var t = f.Session(doubles, tieny, 2500, Feb.AddDays(3));
        f.Row(t, slam, 983047, 1650, 0);
        f.Row(t, gargoyle, 786446, 850, 1);
        var third3 = f.Session(doubles, third, 2000, Feb.AddDays(1));
        f.Row(third3, slam, 900000, 700, 0);
        f.Row(third3, odin, 900000, 650, 1);
        f.Row(third3, gargoyle, 900000, 650, 2);
        var kimBefore = f.Session(oldDoubles, kim, 2400, Feb.AddMonths(-7));
        f.Row(kimBefore, slam, 960000, 1300, 0);
        f.Row(kimBefore, odin, 950000, 1100, 1);
        f.Session(singles, kim, 999, Feb.AddDays(5)); // a Singles session is a different sport
        return new MoMReadHandlerWorld(f, doubles, oldDoubles, k, t, third3, kimBefore, kim.Id, slam.Id, odin.Id);
    }
}
