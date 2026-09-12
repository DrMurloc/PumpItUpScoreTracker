using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Where the levels sit (docs/design/pumbility-overhaul.md D68): how many charts of each level
///     everyone holding your title keeps in their fifty, which levels earn a column, how a mixed pool
///     splits into two, and where the viewer's own fifty sits among them.
/// </summary>
public sealed class PeerLevelSpreadTests
{
    private readonly Dictionary<Guid, Chart> _charts = new();

    [Fact]
    public void AColumnCountsHowManyChartsOfItsLevelEveryHolderHolds()
    {
        // Four holders holding 0, 2, 5 and 9 charts of level 21. The median sits halfway between the middle
        // two, and the middle half runs a quarter and three quarters of the way through the sorted counts.
        var pools = new[] { Pool(), Pool((21, 2)), Pool((21, 5)), Pool((21, 9)) };

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, Array.Empty<Guid>());

        Assert.Equal(4, spread.Peers);
        var column = Assert.Single(spread.Columns);
        Assert.Equal(21, column.Level);
        Assert.Equal(ChartType.Single, column.Type);
        Assert.Equal(new[] { (0, 1), (2, 1), (5, 1), (9, 1) },
            column.PeersByCount.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)));
        Assert.Equal(0, column.Fewest);
        Assert.Equal(9, column.Most);
        Assert.Equal(1.5, column.FirstQuartile, 6);
        Assert.Equal(3.5, column.Median, 6);
        Assert.Equal(6, column.ThirdQuartile, 6);
        Assert.Equal(3, column.Holding);
    }

    [Fact]
    public void ALevelEarnsAColumnAtOneHolderInFiftyOrWhenYouHoldOne()
    {
        // A hundred holders. Two hold a 25, which is one in fifty; one holds a 27, which is not. The viewer
        // alone holds an 18. Every level from the lowest shown to the highest is a column, so the 19 and 20
        // nobody holds still stand between them.
        var pools = Enumerable.Range(0, 100).Select(i => i switch
        {
            0 or 1 => Pool((21, 10), (25, 1)),
            2 => Pool((21, 10), (27, 1)),
            _ => Pool((21, 10))
        }).ToArray();

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, Charts(18, 1));

        Assert.Equal(new[] { 18, 19, 20, 21, 22, 23, 24, 25 }, spread.Columns.Select(c => c.Level));
    }

    [Fact]
    public void AMixedPoolDrawsEachLevelTwice()
    {
        // A pool holding both types: every level draws a singles column beside a doubles one, and nothing is
        // ever added across them. Level 20 is singles-only for the cohort and doubles-only for the viewer,
        // and the two columns say exactly that rather than a single count of three.
        var pools = new[] { Pool((20, 2)).Concat(Charts(21, 3, ChartType.Double)).ToHashSet() };

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, Charts(20, 1, ChartType.Double));

        Assert.Equal(new[]
        {
            (20, ChartType.Single), (20, ChartType.Double), (21, ChartType.Single), (21, ChartType.Double)
        }, spread.Columns.Select(c => (c.Level, c.Type)));
        Assert.Equal(2, Assert.Single(spread.Columns, c => c is { Level: 20, Type: ChartType.Single }).Most);
        Assert.Equal(0, Assert.Single(spread.Columns, c => c is { Level: 20, Type: ChartType.Single }).Mine);
        Assert.Equal(0, Assert.Single(spread.Columns, c => c is { Level: 20, Type: ChartType.Double }).Most);
        Assert.Equal(1, Assert.Single(spread.Columns, c => c is { Level: 20, Type: ChartType.Double }).Mine);
        Assert.Equal(3, Assert.Single(spread.Columns, c => c is { Level: 21, Type: ChartType.Double }).Most);
    }

    [Fact]
    public void WhereYouSitCountsTheHoldersBelowYouAndLevelWithYou()
    {
        var pools = new[] { Pool((22, 1)), Pool((22, 4)), Pool((22, 4)), Pool((22, 7)), Pool() };

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, Charts(22, 4));

        var column = Assert.Single(spread.Columns);
        Assert.Equal(4, column.Mine);
        Assert.Equal(2, column.PeersBelowMine); // the holder holding one, and the holder holding none
        Assert.Equal(2, column.PeersLevelWithMine);
    }

    [Fact]
    public void HoldingNoneOfALevelIsACountLikeAnyOther()
    {
        // Two of three holders hold a 23 and the viewer holds none, which puts them level with the one holder
        // who holds none either.
        var pools = new[] { Pool((23, 3)), Pool((23, 6)), Pool((24, 2)) };

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, Charts(24, 1));

        var column = Assert.Single(spread.Columns, c => c.Level == 23);
        Assert.Equal(0, column.Mine);
        Assert.Equal(1, column.PeersByCount[0]);
        Assert.Equal(0, column.PeersBelowMine);
        Assert.Equal(1, column.PeersLevelWithMine);
    }

    [Fact]
    public void BoardHoldersCountAndAHolderWithNoPoolHoldsNothingAnywhere()
    {
        var board = PeerVoice.FromBoard(7, "BOARD#1234");
        var account = PeerVoice.Account(Guid.NewGuid());
        var poolless = PeerVoice.Account(Guid.NewGuid());
        var pools = new Dictionary<PeerVoice, IReadOnlySet<Guid>>
        {
            [board] = Charts(20, 3).ToHashSet(),
            [account] = Charts(20, 1).ToHashSet()
        };
        var summary = new PeerPoolSummary(new HashSet<PeerVoice> { board, account, poolless }, pools,
            new Dictionary<Guid, PeerPoolChart>());

        var spread = PeerLevelSpread.Of(summary, _charts, Array.Empty<Guid>());

        Assert.Equal(3, spread.Peers);
        Assert.Equal(1, spread.BoardPeers);
        var column = Assert.Single(spread.Columns);
        Assert.Equal(1, column.PeersByCount[0]);
        Assert.Equal(2, column.Holding);
        Assert.Equal(3, column.Most);
    }

    [Fact]
    public void AChartOutsideTheCatalogCountsNowhere()
    {
        var stray = Guid.NewGuid(); // never given a chart
        var pools = new[] { Charts(21, 2).Append(stray).ToHashSet() };

        var spread = PeerLevelSpread.Of(Summary(pools), _charts, new[] { stray });

        var column = Assert.Single(spread.Columns);
        Assert.Equal(2, column.Most);
        Assert.Equal(0, column.Mine);
    }

    [Fact]
    public void WithNoHoldersYourOwnLevelsStillStand()
    {
        var spread = PeerLevelSpread.Of(Summary(Array.Empty<IReadOnlySet<Guid>>()), _charts,
            Charts(20, 2).Concat(Charts(22, 1)));

        Assert.Equal(0, spread.Peers);
        Assert.Equal(new[] { 20, 21, 22 }, spread.Columns.Select(c => c.Level));
        Assert.Equal(2, spread.Columns[0].Mine);
        Assert.All(spread.Columns, c => Assert.Equal(0, c.Median));
    }

    [Fact]
    public void NoHoldersAndNoFiftyMeanNoColumns()
    {
        var spread = PeerLevelSpread.Of(Summary(Array.Empty<IReadOnlySet<Guid>>()), _charts, Array.Empty<Guid>());

        Assert.Empty(spread.Columns);
    }

    [Fact]
    public void ACohortsHalfIsTheSameWhoeverStandsOnIt()
    {
        // The half worth caching carries no viewer at all, so two readers of the same band place different
        // fifties on one reading and get the same cohort back under both.
        var cohort = CohortLevelSpread.Of(Summary(new[] { Pool((21, 2)), Pool((21, 6)) }), _charts);

        var band = Assert.Single(cohort.Bands);
        Assert.Equal(2, cohort.Holders);
        Assert.Equal(2, cohort.HoldingByLevel[21]);
        Assert.Equal(4, band.Median, 6);
        Assert.Equal(4, PeerLevelSpread.Of(cohort, _charts, Charts(21, 4)).Columns[0].Median, 6);
        Assert.Equal(4, PeerLevelSpread.Of(cohort, _charts, Array.Empty<Guid>()).Columns[0].Median, 6);
    }

    /// <summary>A pool holding the given number of fresh singles charts at each level.</summary>
    private IReadOnlySet<Guid> Pool(params (int Level, int Count)[] levels)
    {
        return levels.SelectMany(l => Charts(l.Level, l.Count)).ToHashSet();
    }

    /// <summary>Fresh charts at one level and type, known to the catalog the spread reads them from.</summary>
    private IReadOnlyList<Guid> Charts(int level, int count, ChartType type = ChartType.Single)
    {
        var charts = Enumerable.Range(0, count)
            .Select(_ => new ChartBuilder().WithLevel(level).WithType(type).Build()).ToArray();
        foreach (var chart in charts) _charts[chart.Id] = chart;
        return charts.Select(c => c.Id).ToArray();
    }

    /// <summary>One account holder per pool.</summary>
    private static PeerPoolSummary Summary(IEnumerable<IReadOnlySet<Guid>> pools)
    {
        var byPeer = pools.ToDictionary(_ => PeerVoice.Account(Guid.NewGuid()), p => p);
        return new PeerPoolSummary(byPeer.Keys.ToHashSet(), byPeer, new Dictionary<Guid, PeerPoolChart>());
    }
}
