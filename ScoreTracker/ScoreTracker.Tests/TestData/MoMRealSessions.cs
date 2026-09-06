using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.TestData;

/// <summary>
///     Real March of Murlocs sessions, production-synced (docs/design/march-of-murlocs.md
///     §11.3, §11.10): 김재현's Winter 2025 Doubles session — 39 charts, 59,319 points, 1st of
///     11 — and his August 2024 one (the other file). The same chart in both sessions gets the
///     same id, so the cross-season compare has something to join on.
/// </summary>
internal static partial class MoMRealSessions
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(45);

    private static readonly Dictionary<(string, int), Guid> Ids = new();

    /// <summary>(name, folder level, balanced level, seconds, score, plate, broken, session points, chart bonus)</summary>
    private static readonly (string Name, int Level, double Balanced, int Seconds, int Score, string Plate, bool Broken, int Points, int Bonus)[]
        Winter2025Rows =
        {
            ("Underworld ft. Skizzo (PIU Edit.)", 23, 23.5, 111, 984346, "MG", false, 1472, 0),
            ("Odin", 23, 23.5, 127, 976240, "TG", false, 1565, 0),
            ("Telling Fortune Flower", 23, 23.5, 113, 979705, "MG", false, 1438, 0),
            ("Big Daddy", 23, 23.5, 122, 964066, "MG", false, 1380, 0),
            ("Point Break", 23, 23.5, 101, 971005, "FG", false, 1183, 0),
            ("New Rose", 23, 23.5, 119, 960378, "FG", false, 1325, 0),
            ("Pirate", 21, 21.5, 130, 987859, "MG", false, 1164, 0),
            ("Adrenaline Blaster", 23, 23.98, 119, 976959, "TG", false, 1653, 176),
            ("FLVSH OUT", 23, 23.5, 127, 979784, "TG", false, 1617, 0),
            ("Slam", 24, 24.5, 99, 976489, "FG", false, 1528, 0),
            ("Pneumonoultramicroscopicsilicovolcanoconiosis ft. Kagamine Len/GUMI", 22, 22.5, 121, 972450, "RG", false, 1152, 0),
            ("Kasou Shinja", 24, 24.5, 119, 969602, "TG", false, 1722, 0),
            ("MURDOCH", 24, 24.79, 101, 938435, "RG", true, 1246, 82),
            ("Tomboy", 22, 22.5, 120, 962577, "RG", false, 1081, 0),
            ("Queencard", 22, 22.5, 102, 991718, "MG", false, 1154, 0),
            ("Pop Sequence", 23, 23.5, 119, 960218, "RG", false, 1324, 0),
            ("BRAIN POWER", 24, 24.68, 112, 943862, "RG", false, 1376, 56),
            ("Festival of Death Moon", 23, 23.5, 116, 953445, "RG", false, 1179, 0),
            ("Annihilator Method", 24, 24.5, 110, 958243, "FG", false, 1493, 0),
            ("Perpetual", 24, 24.5, 109, 945530, "FG", false, 1293, 0),
            ("Goodtek", 24, 24.5, 126, 928890, "RG", true, 1393, 0),
            ("Meteo5cience (GADGET mix)", 22, 22.75, 158, 950141, "RG", false, 1301, 74),
            ("Jupin - SHORT CUT -", 23, 23.5, 48, 982585, "TG", false, 626, 0),
            ("DUEL", 24, 24.5, 111, 955044, "FG", false, 1442, 0),
            ("Boca", 25, 25.5, 129, 915890, "RG", true, 1635, 0),
            ("8 6 - FULL SONG -", 23, 23.5, 266, 962566, "RG", false, 2990, 0),
            ("GLORIA", 25, 25.5, 116, 945885, "RG", true, 1711, 0),
            ("Nade Nade", 24, 24.5, 122, 950843, "FG", false, 1492, 0),
            ("Final Audition Ep. 2-X", 24, 25.42, 104, 927315, "RG", false, 1393, 250),
            ("Papasito (feat.  KuTiNA) - FULL SONG -", 23, 23.5, 245, 974365, "FG", false, 2966, 0),
            ("Love is a Danger Zone pt. 2 - SHORT CUT -", 23, 23.5, 58, 973872, "FG", false, 698, 0),
            ("Can-can ~Orpheus in The Party Mix~ - SHORT CUT -", 25, 25.5, 51, 952491, "FG", true, 793, 0),
            ("Break Through Myself feat. Risa Yuzuki", 25, 25.5, 126, 919911, "RG", true, 1643, 0),
            ("MEGAHEARTZ", 25, 25.5, 120, 902836, "RG", true, 1380, 0),
            ("Underworld ft. Skizzo (PIU Edit.)", 25, 25.5, 111, 941971, "RG", true, 1611, 0),
            ("Darkside of The Mind", 25, 25.5, 120, 941887, "RG", false, 1741, 0),
            ("Kokugen Kairou Labyrinth", 26, 26.5, 112, 906317, "RG", true, 1625, 0),
            ("Leather", 26, 26.5, 178, 888018, "RG", true, 2327, 0),
            ("Gargoyle - FULL SONG -", 25, 25.5, 378, 844710, "RG", true, 3207, 0)
        };

    public const int Winter2025Total = 59319;

    public static IReadOnlyList<MoMSessionChart> Winter2025()
    {
        return Winter2025Rows
            .Select(r => new MoMSessionChart(Chart(r.Name, r.Level, r.Seconds), r.Score, Plate(r.Plate),
                r.Broken, r.Points, r.Bonus, r.Balanced, null))
            .ToArray();
    }

    public static Chart Chart(string name, int level, int seconds, ChartType type = ChartType.Double)
    {
        lock (Ids)
        {
            if (!Ids.TryGetValue((name, level), out var id))
            {
                id = Guid.NewGuid();
                Ids[(name, level)] = id;
            }

            return new ChartBuilder().WithId(id).WithLevel(level).WithType(type)
                .WithSong(new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/j.png"),
                    TimeSpan.FromSeconds(seconds), Name.From("Artist"), null))
                .Build();
        }
    }

    public static PhoenixPlate Plate(string shorthand)
    {
        return shorthand switch
        {
            "PG" => PhoenixPlate.PerfectGame,
            "UG" => PhoenixPlate.UltimateGame,
            "EG" => PhoenixPlate.ExtremeGame,
            "SG" => PhoenixPlate.SuperbGame,
            "MG" => PhoenixPlate.MarvelousGame,
            "TG" => PhoenixPlate.TalentedGame,
            "FG" => PhoenixPlate.FairGame,
            _ => PhoenixPlate.RoughGame
        };
    }
}
