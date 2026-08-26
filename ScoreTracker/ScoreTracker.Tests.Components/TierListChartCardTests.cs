using Bunit;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class TierListChartCardTests : ComponentTestBase
{
    // The card nests DifficultyBubble, which gates its tooltip on RendererInfo.IsInteractive
    // and throws when bUnit leaves that unset.
    public TierListChartCardTests()
    {
        this.RenderInteractive();
    }

    [Fact]
    public void TheProjectedLineNamesWhoseNumberItIs()
    {
        // A bare score beside the player's own reads as a target somebody set for them.
        var cut = Render(showProjected: true, projected: 962_400);

        Assert.Contains("962,400", cut.Markup);
        Assert.Contains("projected", cut.Markup);
    }

    [Fact]
    public void AChartNobodyAtYourLevelHasPlayedSaysSoRatherThanGoingBlank()
    {
        // The absence has to be stated. A line that simply vanishes reads as the chart having
        // no data at all, and a zero would be a number nobody produced.
        var cut = Render(showProjected: true, projected: null);

        Assert.Contains("projected", cut.Markup);
        Assert.DoesNotContain("0", cut.Find(".tier-chart-card-meta").TextContent);
    }

    [Fact]
    public void TheLineStaysOffWhenTheSwitchIsOff()
    {
        var cut = Render(showProjected: false, projected: 962_400);

        Assert.DoesNotContain("projected", cut.Markup);
        Assert.DoesNotContain("962,400", cut.Markup);
    }

    private IRenderedComponent<TierListChartCard> Render(bool showProjected, int? projected)
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From("Sarabande"), SongType.Arcade,
                new Uri("https://piuimages.arroweclip.se/probe.png"), TimeSpan.Zero,
                Name.From("Probe"), null),
            ChartType.Double, DifficultyLevel.From(18), MixEnum.Phoenix, null, null);

        return RenderComponent<TierListChartCard>(p => p
            .Add(x => x.Chart, chart)
            .Add(x => x.ShowProjectedScore, showProjected)
            .Add(x => x.ProjectedScore, projected == null ? null : PhoenixScore.From(projected.Value)));
    }
}
