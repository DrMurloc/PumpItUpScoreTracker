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
///     Where the levels sit, as the spread of everyone holding your title
///     (docs/design/pumbility-overhaul.md D68): a column per level — two where the pool holds both types —
///     on one shared scale, the diamond at your count, and the tooltip each column carries.
/// </summary>
public sealed class LevelSpreadChartTests : ComponentTestBase
{
    [Fact]
    public void AMixedPoolDrawsATileEachSinglesFirstAndNeverOnePlotStripingBoth()
    {
        var cut = Render(Spread(12, 0,
            Column(21, ChartType.Double, 1, (0, 6), (2, 6)),
            Column(21, ChartType.Single, 2, (0, 2), (4, 10))));

        var tiles = cut.FindAll(".pmb-spread-tile");
        Assert.Equal(2, tiles.Count);
        Assert.Equal("Singles", tiles[0].QuerySelector(".pmb-spread-label")!.TextContent.Trim());
        Assert.Equal("Doubles", tiles[1].QuerySelector(".pmb-spread-label")!.TextContent.Trim());
        // One column per tile, each in its own type's tone.
        Assert.Contains("is-s", Assert.Single(tiles[0].QuerySelectorAll(".pmb-spread-col")).ClassName);
        Assert.Contains("is-d", Assert.Single(tiles[1].QuerySelectorAll(".pmb-spread-col")).ClassName);
    }

    [Fact]
    public void AOneTypePoolDrawsOneTileWithNoTypeHeadingToRead()
    {
        var cut = Render(Spread(4, 0, Column(20, ChartType.Double, 0, (0, 4))));

        Assert.Single(cut.FindAll(".pmb-spread-tile"));
        Assert.Contains("is-d", Assert.Single(cut.FindAll(".pmb-spread-col")).ClassName);
        // Nothing to tell apart, so the tile carries no label.
        Assert.Empty(cut.FindAll(".pmb-spread-label"));
    }

    [Fact]
    public void AColumnPerLevelWithYourDiamondAtYourCount()
    {
        var cut = Render(Spread(4, 0, Column(20, ChartType.Single, 0, (0, 4)),
            Column(21, ChartType.Single, 2, (0, 1), (2, 1), (5, 1), (9, 1))));

        Assert.Equal(new[] { "20", "21" },
            cut.FindAll(".pmb-spread-level").Select(c => c.TextContent.Trim()));
        // Nobody holds more than nine, so the scale is the shortest one: ten, eleven counts tall. Two charts
        // sit two and a half counts up it.
        Assert.Equal("10", cut.Find(".pmb-spread-plot").GetAttribute("data-top"));
        var columns = cut.FindAll(".pmb-spread-col");
        Assert.Contains($"calc({2.5 / 11 * 100:0.###}% - 4.5px)",
            columns[1].QuerySelector(".pmb-spread-you")!.GetAttribute("style"));
        // Every count a holder holds is a bar, zero included.
        Assert.Equal(4, columns[1].QuerySelectorAll(".pmb-spread-bin").Length);
    }

    [Fact]
    public void OneScaleServesEveryColumnAndStopsAtTheNinetyNinthPercentile()
    {
        // Two hundred holders: one holds forty-nine 22s, the rest eight, and everyone keeps up to twelve 23s.
        // The scale is shared and rounds the 99th percentile up to fifteen, so the forty-nine runs off the
        // top — clipped, marked, and never drawn as a bar.
        var cut = Render(Spread(200, 0, Column(22, ChartType.Single, 3, (8, 199), (49, 1)),
            Column(23, ChartType.Single, 0, (12, 200))));

        Assert.Equal("15", cut.Find(".pmb-spread-plot").GetAttribute("data-top"));
        var columns = cut.FindAll(".pmb-spread-col");
        Assert.NotNull(columns[0].QuerySelector(".pmb-spread-clip"));
        Assert.Null(columns[0].QuerySelector(".pmb-spread-bin[data-k='49']"));
        Assert.Null(columns[1].QuerySelector(".pmb-spread-clip"));
    }

    [Fact]
    public void EachColumnCarriesItsTooltipLines()
    {
        var cut = Render(Spread(4, 0,
            Column(21, ChartType.Single, 5, (0, 1), (2, 1), (5, 1), (9, 1))));

        var body = cut.Find(".pmb-spread-col .pmb-spread-tipbody");
        Assert.True(body.HasAttribute("hidden"));
        var text = body.TextContent;
        Assert.Contains("S21", text);
        Assert.Contains("median · middle half 1.5–6", text);
        Assert.Contains("0–9", text);
        Assert.Contains("75%", text);
        Assert.Contains("You 5", text);
        // Two of four holders hold fewer than five.
        Assert.Contains("· more than 50% of them", text);
    }

    [Fact]
    public void HoldingNoneReadsAlongsideTheHoldersWhoHoldNone()
    {
        var cut = Render(Spread(4, 0, Column(24, ChartType.Double, 0, (0, 1), (1, 3))));

        Assert.Contains("· like the 25% who hold none", cut.Find(".pmb-spread-tipbody").TextContent);
    }

    [Fact]
    public void ThePointerLineTheScriptWritesHasItsWordsAndItsBinsInTheMarkup()
    {
        var cut = Render(Spread(4, 0,
            Column(21, ChartType.Single, 2, (0, 1), (2, 1), (5, 1), (9, 1))));

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
        var cut = Render(new PeerLevelSpread(0, 0, Array.Empty<LevelSpreadColumn>()));

        Assert.Empty(cut.FindAll(".pmb-spread"));
    }

    [Fact]
    public void AColumnWhoseHoldersAllHoldMoreThanTheCapStillDrawsItsWhiskerAndCaret()
    {
        // Twelve hundred counts of one set the scale; the six holders of thirty are too few to move it, and
        // every one of them is off the top of it.
        var cut = Render(Spread(600, 0, Column(21, ChartType.Single, 2, (1, 600)),
            Column(22, ChartType.Single, 0, (1, 594), (30, 6))));

        var tall = cut.FindAll(".pmb-spread-col")[1];
        var whisker = tall.QuerySelector(".pmb-spread-whisker")!.GetAttribute("style")!;
        // The top end clips to the cap, so a whisker running past it never draws a negative height.
        Assert.DoesNotContain("-", whisker);
        Assert.NotNull(tall.QuerySelector(".pmb-spread-clip"));
    }

    [Fact]
    public void TheTopGridlineLandsOnTheCap()
    {
        // Your own thirty-three sets the reach. The axis steps by ten above thirty, so the cap rounds to
        // forty rather than thirty-five, where the top gridline would have had nowhere to land.
        var cut = Render(Spread(4, 0, Column(21, ChartType.Single, 33, (1, 2), (2, 2))));

        Assert.Equal("40", cut.Find(".pmb-spread-plot").GetAttribute("data-top"));
        Assert.Equal(new[] { "0", "10", "20", "30", "40" },
            cut.FindAll(".pmb-spread-gridline b").Select(b => b.TextContent.Trim()).ToArray());
    }

    private IRenderedComponent<LevelSpreadChart> Render(PeerLevelSpread spread)
    {
        return RenderComponent<LevelSpreadChart>(p => p.Add(x => x.Spread, spread));
    }

    private static PeerLevelSpread Spread(int holders, int board, params LevelSpreadColumn[] columns)
    {
        return new PeerLevelSpread(holders, board, columns);
    }

    /// <summary>A column from how many holders hold each count, with the viewer holding <paramref name="mine" />.</summary>
    private static LevelSpreadColumn Column(int level, ChartType type, int mine, params (int Count, int Holders)[] bins)
    {
        var counts = bins.SelectMany(b => Enumerable.Repeat(b.Count, b.Holders)).OrderBy(c => c).ToArray();
        return new LevelSpreadColumn(level, type, bins.ToDictionary(b => b.Count, b => b.Holders),
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
