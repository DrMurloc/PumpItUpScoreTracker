using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.EventCompetition.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The Planner's solver (docs/design/march-of-murlocs.md §11.5), against 김재현's real Doubles
///     record book — 150 charts, production-pulled, each priced at the three energies. Rest per
///     chart is the only control that matters, and this is what it actually does.
/// </summary>
public sealed class MoMPlannerTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(105);

    /// <summary>His banked Winter 2025 session, for the conversion the page prints.</summary>
    private const int Banked = 59319;

    [Fact]
    public void RestPerChartIsTheWholeFeature()
    {
        // Every chart played to your best: the ceiling the page says plainly that it is.
        var expected = new (int Seconds, int Charts, int Points)[]
        {
            (10, 53, 93297),
            (35, 44, 78668),
            (60, 38, 67544),
            (120, 28, 50632)
        };

        var actual = expected
            .Select(e => (e.Seconds, Charts: Set(e.Seconds).Count, Points: Points(Set(e.Seconds))))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThePlanIsACeilingAndTheGapIsTheInterestingPart()
    {
        // He banked 59,319 against a record book that plans 67,544 at his own 60s of rest: an
        // 88% conversion at matched volume, which is what stamina costs him.
        var atSixty = Points(Set(60));

        Assert.Equal(88, (int)Math.Round(100.0 * Banked / atSixty));
    }

    [Fact]
    public void ThePlanClosesOnTheBiggestChartLeftRatherThanOnWhateverTheRatePicked()
    {
        var plan = MoMPlanner.Solve(Book(), Window, TimeSpan.FromSeconds(35));

        var closing = Book().Single(c => c.ChartId == plan.ClosingChartId);
        var unplayed = Book().Where(c => !plan.Set.Contains(c.ChartId)).ToArray();
        Assert.Equal(plan.Set[^1], plan.ClosingChartId);
        Assert.All(unplayed, c => Assert.True(c.Points <= closing.Points));
    }

    [Fact]
    public void TheClosingMoveIsASwapAndNeverAnExtraChart()
    {
        // The greedy already takes one chart past the budget -- that chart IS the overhang §1
        // allows -- so the closing move must not spend the allowance a second time.
        var rest = TimeSpan.FromSeconds(35);
        var plan = MoMPlanner.Solve(Book(), Window, rest);
        var book = Book().ToDictionary(c => c.ChartId);

        var beforeLast = plan.Set.Take(plan.Set.Count - 1)
            .Aggregate(TimeSpan.Zero, (t, id) => t + book[id].Duration + rest);

        Assert.True(beforeLast < Window);
    }

    [Fact]
    public void ThePushCapDropsEverythingAboveIt()
    {
        var steady = MoMPlanner.Solve(Book(), Window, TimeSpan.FromSeconds(35), 23);
        var book = Book().ToDictionary(c => c.ChartId);

        Assert.All(steady.Set, id => Assert.True(book[id].Level <= 23));
        // Capping costs points: the same window filled with easier charts is worth less.
        Assert.True(Points(steady.Set) < Points(Set(35)));
    }

    [Fact]
    public void AWeakerEnergyPlansADifferentSetAndAlwaysALowerTotal()
    {
        var top = Points(Set(35));
        var great = Points(MoMPlanner.Solve(Book(c => c.Great), Window, TimeSpan.FromSeconds(35)).Set,
            c => c.Great);
        var good = Points(MoMPlanner.Solve(Book(c => c.Good), Window, TimeSpan.FromSeconds(35)).Set,
            c => c.Good);

        Assert.True(good < great);
        Assert.True(great < top);
    }

    [Fact]
    public void AChartWorthNothingOrLastingNoTimeIsNeverPlanned()
    {
        var pool = new[]
        {
            new MoMPlanChart(Guid.NewGuid(), 20, TimeSpan.FromSeconds(120), 500),
            new MoMPlanChart(Guid.NewGuid(), 20, TimeSpan.FromSeconds(120), 0),
            new MoMPlanChart(Guid.NewGuid(), 20, TimeSpan.Zero, 900)
        };

        var plan = MoMPlanner.Solve(pool, Window, TimeSpan.FromSeconds(35));

        Assert.Equal(new[] { pool[0].ChartId }, plan.Set);
    }

    [Fact]
    public void AnEmptyBookPlansNothingAndClosesOnNothing()
    {
        var plan = MoMPlanner.Solve(Array.Empty<MoMPlanChart>(), Window, TimeSpan.FromSeconds(35));

        Assert.Empty(plan.Set);
        Assert.Null(plan.ClosingChartId);
    }

    [Fact]
    public void TotalsCountEveryChartsSongAndTheRestBetweenThemButNotAfterTheLast()
    {
        var rest = TimeSpan.FromSeconds(30);
        var pool = new[]
        {
            new MoMPlanChart(Guid.NewGuid(), 20, TimeSpan.FromSeconds(120), 500),
            new MoMPlanChart(Guid.NewGuid(), 20, TimeSpan.FromSeconds(60), 400)
        };

        var (points, song, wall) = MoMPlanner.Totals(pool, rest);

        Assert.Equal(900, points);
        Assert.Equal(TimeSpan.FromSeconds(180), song);
        Assert.Equal(TimeSpan.FromSeconds(210), wall);
        Assert.Equal((0, TimeSpan.Zero, TimeSpan.Zero), MoMPlanner.Totals(Array.Empty<MoMPlanChart>(), rest));
    }

    // ---- the record book ----------------------------------------------------------------------

    private static IReadOnlyList<Guid> Set(int restSeconds) =>
        MoMPlanner.Solve(Book(), Window, TimeSpan.FromSeconds(restSeconds)).Set;

    private static int Points(IReadOnlyList<Guid> set, Func<Priced, int>? energy = null)
    {
        var priced = Priced.All.ToDictionary(c => c.ChartId);
        return set.Sum(id => (energy ?? (c => c.Top))(priced[id]));
    }

    private static IReadOnlyList<MoMPlanChart> Book(Func<Priced, int>? energy = null) =>
        Priced.All
            .Select(c => new MoMPlanChart(c.ChartId, c.Level, TimeSpan.FromSeconds(c.Seconds),
                (energy ?? (x => x.Top))(c)))
            .ToArray();

    /// <summary>One chart of the book at all three energies. Ids are positional and stable.</summary>
    private sealed record Priced(Guid ChartId, int Level, int Seconds, int Top, int Great, int Good)
    {
        public static readonly IReadOnlyList<Priced> All = Rows
            .Select((r, i) => new Priced(new Guid(i + 1, 0, 0, new byte[8]), r.Level, r.Seconds, r.Top,
                r.Great, r.Good))
            .ToArray();
    }

    /// <summary>Level, song seconds, and the points at Top of my game, Great and Good.</summary>
    private static readonly (int Level, int Seconds, int Top, int Great, int Good)[] Rows =
    {
        (25, 111, 2023, 1443, 1208),
        (26, 120, 2155, 2103, 2091),
        (25, 51, 914, 663, 555),
        (24, 99, 1745, 1421, 1261),
        (26, 112, 1964, 1963, 1952),
        (25, 120, 2100, 1560, 1306),
        (25, 120, 2091, 1560, 1306),
        (26, 122, 2123, 2123, 2123),
        (26, 112, 1943, 1943, 1943),
        (24, 104, 1800, 1800, 1619),
        (26, 121, 2023, 2023, 2023),
        (26, 105, 1741, 1741, 1741),
        (24, 104, 1723, 1493, 1324),
        (24, 67, 1108, 962, 853),
        (26, 114, 1874, 1874, 1874),
        (24, 48, 787, 689, 611),
        (24, 98, 1607, 1607, 1466),
        (25, 108, 1751, 1404, 1175),
        (25, 116, 1871, 1508, 1262),
        (24, 104, 1676, 1676, 1644),
        (26, 122, 1966, 1966, 1966),
        (25, 120, 1923, 1560, 1306),
        (26, 131, 2074, 2074, 2074),
        (26, 119, 1877, 1877, 1877),
        (26, 107, 1681, 1681, 1681),
        (26, 121, 1887, 1887, 1887),
        (26, 64, 997, 997, 997),
        (25, 110, 1712, 1430, 1197),
        (25, 109, 1695, 1417, 1186),
        (24, 91, 1412, 1412, 1301),
        (24, 101, 1554, 1553, 1378),
        (25, 105, 1613, 1365, 1143),
        (25, 118, 1808, 1671, 1399),
        (25, 129, 1970, 1710, 1432),
        (24, 99, 1506, 1421, 1261),
        (26, 113, 1716, 1716, 1716),
        (26, 127, 1926, 1926, 1926),
        (25, 129, 1951, 1676, 1404),
        (24, 108, 1629, 1551, 1375),
        (25, 107, 1613, 1536, 1286),
        (25, 113, 1702, 1469, 1230),
        (26, 155, 2325, 2325, 2325),
        (25, 118, 1769, 1534, 1284),
        (25, 127, 1904, 1652, 1383),
        (24, 105, 1567, 1508, 1337),
        (25, 125, 1864, 1625, 1360),
        (25, 127, 1884, 1704, 1426),
        (26, 113, 1671, 1671, 1671),
        (24, 100, 1479, 1436, 1273),
        (25, 92, 1360, 1196, 1001),
        (24, 88, 1297, 1297, 1297),
        (25, 118, 1738, 1738, 1462),
        (25, 121, 1774, 1573, 1317),
        (26, 117, 1711, 1711, 1711),
        (26, 106, 1549, 1549, 1549),
        (24, 110, 1607, 1607, 1607),
        (26, 110, 1597, 1597, 1597),
        (26, 128, 1854, 1854, 1854),
        (24, 119, 1723, 1709, 1515),
        (25, 116, 1677, 1508, 1262),
        (25, 130, 1875, 1689, 1415),
        (25, 121, 1743, 1573, 1317),
        (24, 72, 1036, 1036, 1036),
        (25, 128, 1839, 1663, 1393),
        (25, 111, 1588, 1443, 1208),
        (25, 117, 1672, 1629, 1364),
        (24, 107, 1526, 1526, 1363),
        (24, 110, 1566, 1566, 1401),
        (25, 126, 1793, 1638, 1371),
        (24, 110, 1564, 1564, 1401),
        (25, 121, 1720, 1720, 1483),
        (25, 115, 1631, 1510, 1264),
        (24, 109, 1544, 1544, 1388),
        (24, 103, 1456, 1456, 1456),
        (25, 125, 1763, 1763, 1491),
        (23, 58, 816, 716, 667),
        (25, 74, 1039, 962, 805),
        (25, 109, 1530, 1530, 1333),
        (24, 103, 1446, 1446, 1312),
        (25, 119, 1665, 1547, 1295),
        (23, 48, 670, 592, 552),
        (25, 122, 1701, 1612, 1349),
        (23, 119, 1654, 1644, 1533),
        (25, 118, 1637, 1534, 1284),
        (25, 114, 1572, 1482, 1241),
        (24, 106, 1459, 1459, 1350),
        (23, 62, 846, 765, 713),
        (23, 88, 1201, 1201, 1191),
        (24, 110, 1489, 1489, 1489),
        (25, 116, 1570, 1508, 1262),
        (26, 126, 1693, 1693, 1693),
        (24, 110, 1468, 1468, 1468),
        (26, 147, 1952, 1952, 1952),
        (24, 100, 1324, 1324, 1273),
        (25, 120, 1580, 1580, 1397),
        (25, 121, 1594, 1594, 1359),
        (25, 111, 1460, 1460, 1460),
        (24, 105, 1379, 1379, 1337),
        (26, 178, 2328, 2328, 2328),
        (26, 127, 1655, 1655, 1655),
        (24, 111, 1443, 1443, 1414),
        (25, 119, 1537, 1537, 1295),
        (23, 127, 1617, 1567, 1461),
        (24, 101, 1264, 1264, 1264),
        (24, 101, 1261, 1261, 1261),
        (25, 127, 1567, 1567, 1382),
        (24, 112, 1377, 1377, 1377),
        (24, 122, 1493, 1493, 1493),
        (23, 245, 2966, 2966, 2819),
        (25, 130, 1572, 1572, 1528),
        (24, 93, 1112, 1112, 1112),
        (23, 101, 1183, 1183, 1162),
        (24, 65, 760, 760, 760),
        (25, 107, 1243, 1243, 1164),
        (23, 68, 788, 788, 782),
        (24, 110, 1267, 1267, 1267),
        (27, 125, 1425, 1425, 1425),
        (22, 68, 770, 715, 690),
        (22, 102, 1155, 1073, 1035),
        (23, 122, 1380, 1380, 1380),
        (23, 266, 2990, 2990, 2990),
        (26, 128, 1426, 1426, 1426),
        (23, 119, 1325, 1325, 1325),
        (23, 119, 1324, 1324, 1324),
        (24, 126, 1394, 1394, 1394),
        (23, 74, 814, 814, 814),
        (21, 57, 610, 590, 546),
        (26, 163, 1734, 1734, 1734),
        (23, 58, 616, 616, 616),
        (26, 120, 1260, 1260, 1260),
        (22, 67, 693, 693, 680),
        (22, 56, 578, 578, 568),
        (23, 116, 1179, 1179, 1179),
        (22, 58, 567, 567, 567),
        (22, 59, 563, 563, 563),
        (22, 121, 1153, 1153, 1153),
        (24, 102, 968, 968, 968),
        (22, 120, 1081, 1081, 1081),
        (21, 130, 1164, 1164, 1105),
        (21, 52, 450, 450, 442),
        (21, 51, 437, 437, 433),
        (25, 378, 3208, 3208, 3208),
        (22, 158, 1303, 1303, 1303),
        (21, 54, 403, 403, 403),
        (20, 48, 353, 353, 353),
        (27, 125, 897, 897, 897),
        (21, 58, 374, 374, 374),
        (21, 50, 318, 318, 318),
        (21, 54, 335, 335, 335),
        (20, 38, 215, 215, 215)
    };
}
