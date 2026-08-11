using System.Text.RegularExpressions;
using Bunit;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ProjectionSpreadTests : ComponentTestBase
{
    private static readonly int[] Projections =
        { 913_500, 928_700, 935_200, 944_100, 951_600, 962_400, 971_200, 984_900 };

    [Fact]
    public void TheBandsAreCutWhereTheTierListCutsThem()
    {
        // The point of the object: a band edge here can never sit somewhere the bucketing
        // disagrees with, because both read TierListProcessor.StdDev. Assert against that
        // function rather than a copied constant, so a change to it fails here too.
        var values = Projections.Select(p => (double)p).ToArray();
        var mean = values.Average();
        var sd = TierListProcessor.StdDev(values, false);
        var low = values.Min() - (values.Max() - values.Min()) * 0.06;
        var high = values.Max() + (values.Max() - values.Min()) * 0.06;
        var expected = (mean - sd / 2 - low) / (high - low) * 100.0;

        var cut = RenderWith(Projections.Select(p => (p, (PhoenixScore?)null)).ToArray());

        var edges = Regex.Matches(cut.Markup, @"spread-edge[^>]*left:([0-9.]+)%")
            .Select(m => double.Parse(m.Groups[1].Value))
            .ToArray();
        Assert.Contains(edges, e => Math.Abs(e - expected) < 0.01);
    }

    [Fact]
    public void AnUnplayedChartKeepsItsPositionAndLosesOnlyItsFill()
    {
        // The projection is exactly as real for a chart nobody has touched — what is missing is
        // the player's marker, not the number, so dimming the dot would misstate the data.
        var cut = RenderWith(new[] { (913_500, (PhoenixScore?)null), (951_600, (PhoenixScore?)962_400), (984_900, (PhoenixScore?)979_200) });

        Assert.Equal(1, Regex.Matches(cut.Markup, @"spread-dot spread-unplayed").Count);
        // Two played charts, two personal markers.
        Assert.Equal(2, Regex.Matches(cut.Markup, @"spread-mine").Count);
    }

    [Fact]
    public void EveryChartGetsARowAndAPositionInsideTheTrack()
    {
        var cut = RenderWith(Projections.Select(p => (p, (PhoenixScore?)null)).ToArray());

        Assert.Equal(Projections.Length, Regex.Matches(cut.Markup, @"class=""spread-row""").Count);
        foreach (Match m in Regex.Matches(cut.Markup, @"spread-dot[^>]*left:([0-9.]+)%"))
        {
            var pct = double.Parse(m.Groups[1].Value);
            Assert.InRange(pct, 0, 100);
        }
    }

    [Fact]
    public void NothingRendersWithoutRows()
    {
        // A folder no peer has reached has no axis to draw. The page says so in its own words;
        // an empty pair of axes would be a picture of nothing.
        var cut = RenderComponent<ProjectionSpread>(p => p
            .Add(x => x.Rows, Array.Empty<ProjectionSpread.SpreadRow>()));

        Assert.DoesNotContain("spread-row", cut.Markup);
    }

    private IRenderedComponent<ProjectionSpread> RenderWith((int Projected, PhoenixScore? Mine)[] rows)
    {
        var built = rows
            .Select((r, i) => new ProjectionSpread.SpreadRow($"Chart {i}", r.Projected, r.Mine))
            .ToArray();
        return RenderComponent<ProjectionSpread>(p => p.Add(x => x.Rows, built));
    }
}
