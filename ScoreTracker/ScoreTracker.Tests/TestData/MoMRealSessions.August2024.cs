using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.TestData;

internal static partial class MoMRealSessions
{
    /// <summary>
    ///     김재현's March of Murlocs 2 Doubles session (August 2024): 32 charts, 44,139 points.
    ///     (name, folder level, seconds, score, points then, balanced level in MoM 2, balanced level in Winter 2025)
    /// </summary>
    private static readonly (string Name, int Level, int Seconds, int Score, int Points, double LevelThen, double LevelNow)[]
        August2024Rows =
        {
            ("Chobit Flavor", 24, 113, 938635, 1290, 24.5, 24.513702204840666),
            ("Pump me Amadeus", 24, 98, 923654, 1047, 24.5, 25.222064603513772),
            ("Destr0yer", 24, 161, 930409, 1781, 24.5, 24.73058865912763),
            ("Galaxy Collapse", 24, 124, 909027, 1128, 24.5, 25.5),
            ("Imprinting", 24, 117, 927040, 1277, 24.5, 24.633222599285297),
            ("Canon D - FULL SONG -", 24, 198, 930880, 2195, 24.5, 25.004943372690946),
            ("Dignity", 24, 110, 889258, 851, 24.5, 25.5),
            ("Allegro Con Fuoco - FULL SONG -", 25, 275, 896718, 2540, 25.5, 25.623592651907416),
            ("BRAIN POWER", 24, 112, 939830, 1285, 24.5, 24.677471121464244),
            ("MURDOCH", 24, 101, 919830, 1037, 24.5, 24.794983706029925),
            ("Baroque Virus - FULL SONG -", 23, 318, 979407, 3861, 23.5, 23.5),
            ("Love is a Danger Zone pt. 2", 24, 104, 961337, 1303, 24.5, 24.5),
            ("R.I.P", 24, 127, 927340, 1388, 24.5, 24.5),
            ("Uh-Heung", 24, 120, 937515, 1365, 24.5, 24.5),
            ("Gun Rock", 24, 100, 925521, 1085, 24.5, 24.5),
            ("Fire Noodle Challenge", 25, 150, 875795, 1254, 25.5, 25.5),
            ("PRiMA MATERiA", 24, 127, 936521, 1439, 24.5, 24.5),
            ("Horang Pungryuga", 24, 126, 940987, 1452, 24.5, 24.5),
            ("Further", 25, 118, 899473, 1103, 25.5, 25.5),
            ("Beethoven Virus", 24, 100, 955432, 1221, 24.5, 24.5),
            ("MEGAHEARTZ", 25, 120, 897306, 1111, 25.5, 25.5),
            ("Love is a Danger Zone pt. 2 - SHORT CUT -", 23, 58, 976199, 683, 23.5, 23.5),
            ("Solfeggietto", 25, 105, 897335, 972, 25.5, 25.5),
            ("Phalanx \"RS2018 edit\"", 24, 118, 955034, 1438, 24.5, 24.5),
            ("Kasou Shinja", 24, 119, 942707, 1380, 24.5, 24.5),
            ("The Quick Brown Fox Jumps Over The Lazy Dog", 24, 110, 925720, 1195, 24.5, 24.5),
            ("Hercules", 24, 117, 928554, 1285, 24.5, 24.5),
            ("ERRORCODE: 0", 24, 182, 914721, 1768, 24.5, 24.5),
            ("Can-can ~Orpheus in The Party Mix~ - SHORT CUT -", 25, 51, 938453, 671, 25.5, 25.5),
            ("Annihilator Method", 24, 110, 940900, 1267, 24.5, 24.5),
            ("A Site De La Rue", 24, 99, 943306, 1151, 24.5, 24.5),
            ("Vector", 24, 113, 943765, 1316, 24.5, 24.5)
        };

    public const int August2024Total = 44139;

    public static IReadOnlyList<MoMSessionChart> August2024()
    {
        return August2024Rows
            .Select(r => new MoMSessionChart(Chart(r.Name, r.Level, r.Seconds), r.Score, PhoenixPlate.RoughGame,
                false, r.Points, 0, r.LevelThen, null))
            .ToArray();
    }

    /// <summary>The MoM 2 board's frozen configuration (its level table and its 2024 grade table), with its balance.</summary>
    public static TournamentConfiguration MoM2Season()
    {
        var scoring = MoMTables(new Dictionary<int, int>
            { [20] = 650, [21] = 760, [22] = 930, [23] = 1110, [24] = 1300, [25] = 1500, [26] = 1710, [27] = 1930 });
        scoring.LetterGradeModifiers[PhoenixLetterGrade.A] = .25;
        scoring.LetterGradeModifiers[PhoenixLetterGrade.AAPlus] = 1.0;
        scoring.LetterGradeModifiers[PhoenixLetterGrade.AAA] = 1.1;
        scoring.ChartLevelSnapshot = Snapshot(r => r.LevelThen);
        return Season(scoring, "March of Murlocs 2");
    }

    /// <summary>The Winter 2025 board's frozen configuration (§4's tables), with its balance.</summary>
    public static TournamentConfiguration Winter2025Season()
    {
        var scoring = MoMTables(new Dictionary<int, int>
            { [20] = 650, [21] = 760, [22] = 930, [23] = 1160, [24] = 1450, [25] = 1800, [26] = 2210, [27] = 2680 });
        scoring.ChartLevelSnapshot = Snapshot(r => r.LevelNow);
        return Season(scoring, "Winter 2025");
    }

    private static ScoringConfiguration MoMTables(IReadOnlyDictionary<int, int> levelRatings)
    {
        var scoring = ScoringConfiguration.PumbilityPlus;
        scoring.AdjustToTime = true;
        foreach (var (level, rating) in levelRatings) scoring.LevelRatings[DifficultyLevel.From(level)] = rating;
        foreach (var type in scoring.ChartTypeModifiers.Keys.Where(t => t != ChartType.Double).ToArray())
            scoring.ChartTypeModifiers[type] = 0;
        return scoring;
    }

    // Delta rows only, as the season snapshot stores them (§9.3): a chart at folder + 0.5 has no row.
    private static Dictionary<Guid, double> Snapshot(Func<(string Name, int Level, int Seconds, int Score, int Points, double LevelThen, double LevelNow), double> level)
    {
        return August2024Rows
            .Where(r => Math.Abs(level(r) - (r.Level + .5)) > 0.0001)
            .ToDictionary(r => Chart(r.Name, r.Level, r.Seconds).Id, level);
    }

    private static TournamentConfiguration Season(ScoringConfiguration scoring, string name)
    {
        return new TournamentConfiguration(Guid.NewGuid(), Name.From(name), scoring, false, true) { MaxTime = Window };
    }
}
