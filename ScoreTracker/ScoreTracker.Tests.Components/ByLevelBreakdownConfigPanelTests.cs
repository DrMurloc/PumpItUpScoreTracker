using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ByLevelBreakdownConfigPanelTests : ComponentTestBase
{
    // ---- adaptive rules (pure) ----

    [Fact]
    public void LegacyMixesOfferOnlyGradeAndPass()
    {
        Assert.Equal(4, ByLevelConfigRules.MetricsFor(null).Count);
        Assert.Equal(4, ByLevelConfigRules.MetricsFor(MixEnum.Phoenix2).Count);
        Assert.Equal(new[] { BreakdownMetric.LetterGrade, BreakdownMetric.Pass },
            ByLevelConfigRules.MetricsFor(MixEnum.XX));
        Assert.Equal(new[] { BreakdownMetric.LetterGrade, BreakdownMetric.Pass },
            ByLevelConfigRules.MetricsFor(MixEnum.Prime2));
    }

    [Fact]
    public void MetricRestrictsAggregations()
    {
        Assert.DoesNotContain(BreakdownAggregation.Breakdown,
            ByLevelConfigRules.AggregationsFor(BreakdownMetric.Score));
        Assert.DoesNotContain(BreakdownAggregation.Distribution,
            ByLevelConfigRules.AggregationsFor(BreakdownMetric.Pass));
        Assert.Equal(3, ByLevelConfigRules.AggregationsFor(BreakdownMetric.LetterGrade).Count);
    }

    // ---- the panel end-to-end ----

    private static HomePageWidgetRecord Widget(string configJson = "") =>
        new(Guid.NewGuid(), "by-level-breakdown", null, 0, "2x2", configJson, 1);

    private IRenderedComponent<ByLevelBreakdownConfigPanel> Render(HomePageWidgetRecord widget,
        Action<(string Json, int Version)> onSave)
    {
        return RenderComponent<ByLevelBreakdownConfigPanel>(p => p
            .Add(x => x.Widget, widget)
            .Add(x => x.OnSave, EventCallback.Factory.Create<(string, int)>(this, t => onSave((t.Item1, t.Item2))))
            .Add(x => x.OnCancel, EventCallback.Empty));
    }

    [Fact]
    public void SaveEmitsTheDefaultConfig()
    {
        (string Json, int Version)? saved = null;
        var cut = Render(Widget(), t => saved = t);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Save")).Click();

        Assert.NotNull(saved);
        var config = WidgetConfigJson.Read<ByLevelBreakdownConfig>(saved!.Value.Json);
        Assert.Equal(BreakdownMetric.Score, config.Metric);
        Assert.Equal(BreakdownAggregation.Distribution, config.Aggregation);
        Assert.Equal(17, config.MinLevel);
        Assert.Equal(23, config.MaxLevel);
    }

    [Fact]
    public void PinningALegacyMixCoercesAScoreMetricToGrade()
    {
        var pinnedXxWithScore = WidgetConfigJson.Write(new ByLevelBreakdownConfig
        {
            Mix = MixEnum.XX,
            Metric = BreakdownMetric.Score, // not valid for a legacy mix
            Aggregation = BreakdownAggregation.Distribution
        });
        (string Json, int Version)? saved = null;
        var cut = Render(Widget(pinnedXxWithScore), t => saved = t);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Save")).Click();

        var config = WidgetConfigJson.Read<ByLevelBreakdownConfig>(saved!.Value.Json);
        Assert.Equal(BreakdownMetric.LetterGrade, config.Metric); // coerced on load
        Assert.Contains(config.Aggregation, ByLevelConfigRules.AggregationsFor(config.Metric));
    }
}
