using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.Domain.Services;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Where the levels sit, as your peers' spread (docs/design/pumbility-overhaul.md D67): a tile per lit
///     type, a column per level on one shared scale, the diamond at your count, and the tooltip each column
///     carries.
/// </summary>
public sealed class LevelSpreadChartTests : ComponentTestBase
{
    [Fact]
    public void ATileForEachTypeSinglesFirstWithWhoItCounts()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Double] = Spread(12, 0, Column(22, 1, (0, 4), (3, 8))),
            [ChartType.Single] = Spread(395, 340, Column(21, 2, (0, 95), (5, 300)))
        });

        var tiles = cut.FindAll(".pmb-spread-tile");
        Assert.Equal(2, tiles.Count);
        Assert.Equal("Singles", tiles[0].QuerySelector(".pmb-spread-label")!.TextContent.Trim());
        Assert.Equal("395 peers · 340 from the official board", tiles[0].QuerySelector(".pmb-spread-peers")!.TextContent.Trim());
        Assert.Equal("Doubles", tiles[1].QuerySelector(".pmb-spread-label")!.TextContent.Trim());
        // No board peers, no board note.
        Assert.Equal("12 peers", tiles[1].QuerySelector(".pmb-spread-peers")!.TextContent.Trim());
    }

    [Fact]
    public void AColumnPerLevelWithYourDiamondAtYourCount()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Single] = Spread(4, 0, Column(20, 0, (0, 4)), Column(21, 2, (0, 1), (2, 1), (5, 1), (9, 1)))
        });

        var columns = cut.FindAll(".pmb-spread-col");
        Assert.Equal(new[] { "20", "21" }, columns.Select(c => c.QuerySelector(".pmb-spread-level")!.TextContent.Trim()));
        // Nobody keeps more than nine, so the scale is the shortest one: ten, eleven counts tall. Two charts
        // sit two and a half counts up it.
        Assert.Equal("10", cut.Find(".pmb-spread-plot").GetAttribute("data-top"));
        Assert.Contains($"calc({2.5 / 11 * 100:0.###}% - 4.5px)",
            columns[1].QuerySelector(".pmb-spread-you")!.GetAttribute("style"));
        // Every count a peer keeps is a bar, zero included.
        Assert.Equal(4, columns[1].QuerySelectorAll(".pmb-spread-bin").Length);
    }

    [Fact]
    public void OneScaleServesEveryTileAndStopsAtTheNinetyNinthPercentile()
    {
        // Two hundred singles peers: one keeps forty-nine 22s, the rest eight. The doubles tile's peers keep
        // up to twelve. The scale is shared and rounds the 99th percentile up to five, so the forty-nine runs
        // off the top — clipped, marked, and never drawn as a bar.
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Single] = Spread(200, 0, Column(22, 3, (8, 199), (49, 1))),
            [ChartType.Double] = Spread(20, 0, Column(23, 0, (12, 20)))
        });

        Assert.All(cut.FindAll(".pmb-spread-plot"), plot => Assert.Equal("15", plot.GetAttribute("data-top")));
        var singles = cut.Find("[data-testid=level-spread-Single] .pmb-spread-col");
        Assert.NotNull(singles.QuerySelector(".pmb-spread-clip"));
        Assert.Null(singles.QuerySelector(".pmb-spread-bin[data-k='49']"));
        Assert.Null(cut.Find("[data-testid=level-spread-Double] .pmb-spread-col").QuerySelector(".pmb-spread-clip"));
    }

    [Fact]
    public void EachColumnCarriesItsTooltipLines()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Single] = Spread(4, 0, Column(21, 5, (0, 1), (2, 1), (5, 1), (9, 1)))
        });

        var body = cut.Find(".pmb-spread-col .pmb-spread-tipbody");
        Assert.True(body.HasAttribute("hidden"));
        var text = body.TextContent;
        Assert.Contains("S21 · your peers", text);
        Assert.Contains("median · middle half 1.5–6", text);
        Assert.Contains("0–9", text);
        Assert.Contains("75%", text);
        Assert.Contains("You 5", text);
        // Two of four peers keep fewer than five.
        Assert.Contains("· more than 50% of your peers", text);
    }

    [Fact]
    public void KeepingNoneReadsAlongsideThePeersWhoKeepNone()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Double] = Spread(4, 0, Column(24, 0, (0, 1), (1, 3)))
        });

        Assert.Contains("· like the 25% who keep none", cut.Find(".pmb-spread-tipbody").TextContent);
    }

    [Fact]
    public void ThePointerLineTheScriptWritesHasItsWordsAndItsBinsInTheMarkup()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Single] = Spread(4, 0, Column(21, 2, (0, 1), (2, 1), (5, 1), (9, 1)))
        });

        var root = cut.Find(".pmb-spread");
        Assert.Equal("1 chart", root.GetAttribute("data-chart-one"));
        Assert.Equal("{0} charts", root.GetAttribute("data-charts"));
        Assert.Equal("· 1 peer ({0}%)", root.GetAttribute("data-peer-one"));
        Assert.Equal("· {0} peers ({1}%)", root.GetAttribute("data-peers"));
        var column = cut.Find(".pmb-spread-col");
        Assert.Equal("0:1,2:1,5:1,9:1", column.GetAttribute("data-bins"));
        Assert.Equal("4", column.GetAttribute("data-peers"));
    }

    [Fact]
    public void NothingToSpreadDrawsNothing()
    {
        var cut = Render(new Dictionary<ChartType, PeerLevelSpread>
        {
            [ChartType.Single] = new(0, 0, Array.Empty<LevelSpreadColumn>())
        });

        Assert.Empty(cut.FindAll(".pmb-spread"));
    }

    private IRenderedComponent<LevelSpreadChart> Render(IReadOnlyDictionary<ChartType, PeerLevelSpread> spreads)
    {
        return RenderComponent<LevelSpreadChart>(p => p.Add(x => x.Spreads, spreads));
    }

    private static PeerLevelSpread Spread(int peers, int board, params LevelSpreadColumn[] columns)
    {
        return new PeerLevelSpread(peers, board, columns);
    }

    /// <summary>A column from how many peers keep each count, with the viewer keeping <paramref name="mine" />.</summary>
    private static LevelSpreadColumn Column(int level, int mine, params (int Count, int Peers)[] bins)
    {
        var counts = bins.SelectMany(b => Enumerable.Repeat(b.Count, b.Peers)).OrderBy(c => c).ToArray();
        return new LevelSpreadColumn(level, bins.ToDictionary(b => b.Count, b => b.Peers),
            counts[0], Quantile(counts, 0.25), Quantile(counts, 0.5), Quantile(counts, 0.75), counts[^1],
            counts.Count(c => c > 0), mine, counts.Count(c => c < mine), counts.Count(c => c == mine));
    }

    private static double Quantile(int[] sorted, double q)
    {
        var position = (sorted.Length - 1) * q;
        var below = (int)Math.Floor(position);
        var above = (int)Math.Ceiling(position);
        return sorted[below] + (sorted[above] - sorted[below]) * (position - below);
    }
}
