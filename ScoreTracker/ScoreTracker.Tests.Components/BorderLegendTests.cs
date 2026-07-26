using Bunit;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The legend prints the card border language, so it has to track what the cards actually
///     draw: the tier list's per-player switch can turn the cross-mix outline off, and a legend
///     entry for a border on no card is noise.
/// </summary>
public sealed class BorderLegendTests : ComponentTestBase
{
    [Fact]
    public void AllThreeBordersArePrintedByDefault()
    {
        var cut = RenderComponent<BorderLegend>();

        Assert.Equal(3, cut.FindAll(".border-legend-item").Count);
        Assert.Single(cut.FindAll(".border-legend-swatch.tier-chart-card-other-mix"));
    }

    [Fact]
    public void HidingCrossMixPassesDropsItsLegendItemAndLeavesTheOtherTwo()
    {
        var cut = RenderComponent<BorderLegend>(p => p.Add(x => x.ShowOtherMix, false));

        Assert.Equal(2, cut.FindAll(".border-legend-item").Count);
        Assert.Empty(cut.FindAll(".border-legend-swatch.tier-chart-card-other-mix"));
        Assert.Single(cut.FindAll(".border-legend-swatch.tier-chart-card-pass"));
        Assert.Single(cut.FindAll(".border-legend-swatch.tier-chart-card-todo"));
    }
}
