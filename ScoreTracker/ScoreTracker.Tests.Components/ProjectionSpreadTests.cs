using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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
        var cut = RenderWith(new[]
        {
            (913_500, (PhoenixScore?)null), (951_600, (PhoenixScore?)962_400),
            (984_900, (PhoenixScore?)979_200)
        });

        Assert.Single(Regex.Matches(cut.Markup, @"spread-shape spread-unplayed"));
        // Two played charts, two personal markers.
        Assert.Equal(2, Regex.Matches(cut.Markup, @"spread-mine").Count);
    }

    [Fact]
    public void EveryChartGetsARowAndAPositionInsideTheTrack()
    {
        var cut = RenderWith(Projections.Select(p => (p, (PhoenixScore?)null)).ToArray());

        Assert.Equal(Projections.Length, Regex.Matches(cut.Markup, @"class=""spread-row""").Count);
        foreach (Match m in Regex.Matches(cut.Markup, @"spread-marker[^>]*left:([0-9.]+)%"))
        {
            var pct = double.Parse(m.Groups[1].Value);
            Assert.InRange(pct, 0, 100);
        }
    }

    [Fact]
    public void AScoreBelowTheFolderIsPinnedToTheEdgeRatherThanStretchingTheAxis()
    {
        // The axis is the spread of the PROJECTIONS — that is the thing being explained. One
        // score far under the folder used to drag the low end down with it and squash every
        // chart into the right-hand third, so an off-scale score keeps its row and loses only
        // its position.
        var withDisaster = RenderWith(new[]
        {
            (913_500, (PhoenixScore?)812_300), (951_600, (PhoenixScore?)948_000),
            (984_900, (PhoenixScore?)979_200)
        });
        var withoutIt = RenderWith(new[]
        {
            (913_500, (PhoenixScore?)910_000), (951_600, (PhoenixScore?)948_000),
            (984_900, (PhoenixScore?)979_200)
        });

        Assert.Single(Regex.Matches(withDisaster.Markup, @"spread-off-left"));
        Assert.Empty(Regex.Matches(withoutIt.Markup, @"spread-off"));
        // Same projections, so the bands must land in exactly the same places either way.
        Assert.Equal(EdgesOf(withoutIt.Markup), EdgesOf(withDisaster.Markup));
    }

    [Fact]
    public async Task TheNumbersLiveOnTheMarkersRatherThanInAColumn()
    {
        // The value column was most of the track's width. Its removal is the reason the markers
        // carry readouts at all, so a column creeping back means the readouts are redundant and
        // nobody notices until the track is a third of the row again.
        //
        // A tooltip renders its content only once shown, so the readout has to be opened to be
        // read — which is the behaviour a phone depends on anyway, there being no hover there.
        var cut = RenderWithPopovers(new[]
        {
            (913_500, (PhoenixScore?)902_100), (951_600, (PhoenixScore?)948_000),
            (984_900, (PhoenixScore?)979_200)
        });

        Assert.DoesNotContain("spread-value", cut.Markup);
        Assert.DoesNotContain("spread-readout", cut.Markup);

        // Three projections and three personal scores: six markers, each its own readout.
        Assert.Equal(6, cut.FindAll(".spread-marker").Count);

        // The tooltip root INSIDE the marker, because that is what carries the handler — the
        // marker span is ours and deliberately has no events of its own. Pointer-up, not click:
        // MudTooltip's ShowOnClick binds onpointerup, so ClickAsync fails here claiming the
        // element has no onclick handler, which reads as a missing handler rather than the
        // wrong event name.
        var anchors = cut.FindAll(".spread-marker .mud-tooltip-root");
        Assert.Equal(6, anchors.Count);
        await anchors[0].TriggerEventAsync("onpointerup", new PointerEventArgs());

        Assert.Contains("spread-readout", cut.Markup);
        Assert.Contains("913,500", cut.Markup);
    }

    [Fact]
    public void OneLowOutlierCannotHandTheOuterBandHalfTheTrack()
    {
        // The bands past ±1.5σ are open-ended, so an axis drawn to the furthest projection gives
        // "1+ Level Harder" whatever the worst chart in the folder is worth. A real D18 folder
        // with a floor 4σ down rendered it at 46% of the track holding two charts, with the five
        // bands holding everything else at 7% each. Capping the axis at ±2σ bounds the outer
        // bands to half a sigma — never wider than Medium, which is a whole one.
        var tightWithAnOutlier = new[]
        {
            812_000, 905_000, 912_000, 918_000, 921_000, 924_000,
            927_000, 930_000, 933_000, 936_000, 941_000, 947_000
        };

        var cut = RenderWith(tightWithAnOutlier.Select(p => (p, (PhoenixScore?)null)).ToArray());

        var widths = Regex.Matches(cut.Markup, @"spread-region""[^>]*width:([0-9.]+)%")
            .Select(m => double.Parse(m.Groups[1].Value))
            .ToArray();
        Assert.NotEmpty(widths);
        // Medium is the 1σ band and therefore the widest the geometry allows; every other band
        // is a half sigma or a clamped remnant of one.
        Assert.All(widths, w => Assert.True(w <= 33.4,
            $"a band took {w:N1}% of the track — the axis is being stretched by an outlier again"));

        // ...and the outlier is still stated rather than quietly parked on the floor.
        Assert.Single(Regex.Matches(cut.Markup, "spread-off-left"));
    }

    [Fact]
    public void TheMarkerIsOurOwnElementRatherThanTheTooltipsRoot()
    {
        // Putting spread-marker on MudTooltip's root via RootClass shipped once and rendered as
        // hairlines and specks: that root carries .mud-tooltip-root{width:auto} and
        // .mud-tooltip-inline{display:inline-block}, both single-class selectors, so the marker's
        // own width and display lost the tie and the shape — an inline span — ignored its size
        // outright. Nothing in a rendered-markup assertion can see that, because the class IS
        // there and the geometry is not bUnit's to compute. What IS visible is the sharing.
        var cut = RenderWithPopovers(new[] { (913_500, (PhoenixScore?)902_100) });

        var markers = cut.FindAll(".spread-marker");
        Assert.NotEmpty(markers);
        foreach (var marker in markers)
        {
            Assert.DoesNotContain("mud-tooltip", marker.ClassName ?? "");
            // ...and the tooltip is inside it, so the readout still has an anchor.
            Assert.NotNull(marker.QuerySelector(".mud-tooltip-root"));
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

    private static double[] EdgesOf(string markup)
    {
        return Regex.Matches(markup, @"spread-edge[^>]*left:([0-9.]+)%")
            .Select(m => double.Parse(m.Groups[1].Value))
            .ToArray();
    }

    private IRenderedComponent<ProjectionSpread> RenderWith((int Projected, PhoenixScore? Mine)[] rows)
    {
        // Interactive: the readouts are MudTooltips, which need the popover provider, and a
        // static render drops them by design.
        this.RenderInteractive();
        return RenderComponent<ProjectionSpread>(p => p
            .Add(x => x.Rows, Build(rows))
            .Add(x => x.Mix, MixEnum.Phoenix));
    }

    /// <summary>
    ///     With a popover provider alongside, the way the live page carries one. A tooltip's
    ///     content renders into the provider rather than into the anchor, so a fragment without
    ///     one can only see the shapes.
    /// </summary>
    private IRenderedFragment RenderWithPopovers((int Projected, PhoenixScore? Mine)[] rows)
    {
        this.RenderInteractive();
        var built = Build(rows);
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ProjectionSpread>(1);
            builder.AddAttribute(2, nameof(ProjectionSpread.Rows), built);
            builder.AddAttribute(3, nameof(ProjectionSpread.Mix), MixEnum.Phoenix);
            builder.CloseComponent();
        });
    }

    private static ProjectionSpread.SpreadRow[] Build((int Projected, PhoenixScore? Mine)[] rows)
    {
        return rows
            .Select((r, i) => new ProjectionSpread.SpreadRow(ChartNamed($"Chart {i}"), r.Projected, r.Mine))
            .ToArray();
    }

    private static Chart ChartNamed(string name)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromSeconds(125), "BanYa", Bpm.From(160, 160)),
            ScoreTracker.SharedKernel.Enums.ChartType.Double, 18, MixEnum.Phoenix, "SUNNY", 700,
            new HashSet<Skill>());
    }
}
