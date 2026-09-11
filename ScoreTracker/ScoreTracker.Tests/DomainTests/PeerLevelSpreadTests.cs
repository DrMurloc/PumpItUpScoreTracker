using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Where the levels sit (docs/design/pumbility-overhaul.md D67): how many charts of each level every peer
///     holds in their fifty, which levels earn a column, and where the viewer's own fifty sits among them.
/// </summary>
public sealed class PeerLevelSpreadTests
{
    private readonly Dictionary<Guid, int> _levels = new();

    [Fact]
    public void AColumnCountsHowManyChartsOfItsLevelEveryPeerHolds()
    {
        // Four peers holding 0, 2, 5 and 9 charts of level 21. The median sits halfway between the middle
        // two, and the middle half runs a quarter and three quarters of the way through the sorted counts.
        var pools = new[] { Pool(), Pool((21, 2)), Pool((21, 5)), Pool((21, 9)) };

        var spread = PeerLevelSpread.Of(Summary(pools), _levels, Array.Empty<Guid>());

        Assert.Equal(4, spread.Peers);
        var column = Assert.Single(spread.Columns);
        Assert.Equal(21, column.Level);
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
    public void ALevelEarnsAColumnAtOnePeerInFiftyOrWhenYouHoldOne()
    {
        // A hundred peers. Two hold a 25, which is one in fifty; one holds a 27, which is not. The viewer alone
        // holds an 18. Every level from the lowest shown to the highest is a column, so the 19 and 20 nobody
        // holds still stand between them.
        var pools = Enumerable.Range(0, 100).Select(i => i switch
        {
            0 or 1 => Pool((21, 10), (25, 1)),
            2 => Pool((21, 10), (27, 1)),
            _ => Pool((21, 10))
        }).ToArray();

        var spread = PeerLevelSpread.Of(Summary(pools), _levels, Charts(18, 1));

        Assert.Equal(new[] { 18, 19, 20, 21, 22, 23, 24, 25 }, spread.Columns.Select(c => c.Level));
    }

    [Fact]
    public void WhereYouSitCountsThePeersBelowYouAndLevelWithYou()
    {
        var pools = new[] { Pool((22, 1)), Pool((22, 4)), Pool((22, 4)), Pool((22, 7)), Pool() };

        var spread = PeerLevelSpread.Of(Summary(pools), _levels, Charts(22, 4));

        var column = Assert.Single(spread.Columns);
        Assert.Equal(4, column.Mine);
        Assert.Equal(2, column.PeersBelowMine); // the peer holding one, and the peer holding none
        Assert.Equal(2, column.PeersLevelWithMine);
    }

    [Fact]
    public void HoldingNoneOfALevelIsACountLikeAnyOther()
    {
        // Two of three peers hold a 23 and the viewer holds none, which puts them level with the one peer who
        // holds none either.
        var pools = new[] { Pool((23, 3)), Pool((23, 6)), Pool((24, 2)) };

        var spread = PeerLevelSpread.Of(Summary(pools), _levels, Charts(24, 1));

        var column = Assert.Single(spread.Columns, c => c.Level == 23);
        Assert.Equal(0, column.Mine);
        Assert.Equal(1, column.PeersByCount[0]);
        Assert.Equal(0, column.PeersBelowMine);
        Assert.Equal(1, column.PeersLevelWithMine);
    }

    [Fact]
    public void BoardPeersCountAndAPeerWithNoPoolHoldsNothingAnywhere()
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

        var spread = PeerLevelSpread.Of(summary, _levels, Array.Empty<Guid>());

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
        var stray = Guid.NewGuid(); // never given a level
        var pools = new[] { Charts(21, 2).Append(stray).ToHashSet() };

        var spread = PeerLevelSpread.Of(Summary(pools), _levels, new[] { stray });

        var column = Assert.Single(spread.Columns);
        Assert.Equal(2, column.Most);
        Assert.Equal(0, column.Mine);
    }

    [Fact]
    public void WithNoPeersYourOwnLevelsStillStand()
    {
        var spread = PeerLevelSpread.Of(Summary(Array.Empty<IReadOnlySet<Guid>>()), _levels,
            Charts(20, 2).Concat(Charts(22, 1)));

        Assert.Equal(0, spread.Peers);
        Assert.Equal(new[] { 20, 21, 22 }, spread.Columns.Select(c => c.Level));
        Assert.Equal(2, spread.Columns[0].Mine);
        Assert.All(spread.Columns, c => Assert.Equal(0, c.Median));
    }

    [Fact]
    public void NoPeersAndNoFiftyMeanNoColumns()
    {
        var spread = PeerLevelSpread.Of(Summary(Array.Empty<IReadOnlySet<Guid>>()), _levels, Array.Empty<Guid>());

        Assert.Empty(spread.Columns);
    }

    /// <summary>A pool holding the given number of fresh charts at each level.</summary>
    private IReadOnlySet<Guid> Pool(params (int Level, int Count)[] levels)
    {
        return levels.SelectMany(l => Charts(l.Level, l.Count)).ToHashSet();
    }

    /// <summary>Fresh charts at one level, known to the catalog the spread reads levels from.</summary>
    private IReadOnlyList<Guid> Charts(int level, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids) _levels[id] = level;
        return ids;
    }

    /// <summary>One account peer per pool.</summary>
    private static PeerPoolSummary Summary(IEnumerable<IReadOnlySet<Guid>> pools)
    {
        var byPeer = pools.ToDictionary(_ => PeerVoice.Account(Guid.NewGuid()), p => p);
        return new PeerPoolSummary(byPeer.Keys.ToHashSet(), byPeer, new Dictionary<Guid, PeerPoolChart>());
    }
}
