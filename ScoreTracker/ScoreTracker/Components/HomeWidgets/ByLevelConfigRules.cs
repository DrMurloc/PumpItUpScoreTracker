using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     The adaptive-config rules for the By-Level Breakdown widget, factored out of the
///     panel so they are unit-testable directly (C4). The mix restricts which metrics are
///     available (legacy scoring → Letter Grade + Pass only, since older mixes have no
///     1M-normalized score and no plates), and the metric restricts which aggregations
///     make sense.
/// </summary>
public static class ByLevelConfigRules
{
    public static IReadOnlyList<BreakdownMetric> MetricsFor(MixEnum? mix) =>
        mix is { } m && m.UsesLegacyScoring()
            ? new[] { BreakdownMetric.LetterGrade, BreakdownMetric.Pass, BreakdownMetric.ChartAge }
            : new[]
            {
                BreakdownMetric.Score, BreakdownMetric.LetterGrade, BreakdownMetric.Plate, BreakdownMetric.Pass,
                BreakdownMetric.ChartAge
            };

    /// <summary>
    ///     A stable, config-driven instance title (localization key) so three rapid-fired
    ///     drawer presets of one widget type wear distinct names (D10 / DynamicNameKey).
    /// </summary>
    public static string TitleKey(BreakdownMetric metric, BreakdownAggregation aggregation) => (metric, aggregation) switch
    {
        (BreakdownMetric.Score, BreakdownAggregation.Distribution) => "Score Distribution",
        (BreakdownMetric.Score, BreakdownAggregation.Completion) => "Score Completion",
        (BreakdownMetric.LetterGrade, BreakdownAggregation.Breakdown) => "Grade Distribution",
        (BreakdownMetric.LetterGrade, BreakdownAggregation.Completion) => "Grade Completion",
        (BreakdownMetric.LetterGrade, BreakdownAggregation.Distribution) => "Average Grade by Level",
        (BreakdownMetric.Plate, BreakdownAggregation.Breakdown) => "Plate Distribution",
        (BreakdownMetric.Plate, BreakdownAggregation.Completion) => "Plate Completion",
        (BreakdownMetric.Plate, BreakdownAggregation.Distribution) => "Average Plate by Level",
        (BreakdownMetric.Pass, BreakdownAggregation.Breakdown) => "Pass Breakdown",
        (BreakdownMetric.Pass, BreakdownAggregation.Completion) => "Clear Progress",
        (BreakdownMetric.ChartAge, _) => "Chart Age by Level",
        _ => "By-Level Breakdown"
    };

    public static IReadOnlyList<BreakdownAggregation> AggregationsFor(BreakdownMetric metric) => metric switch
    {
        // Score is continuous → distribution or threshold completion (grades ARE its bands).
        BreakdownMetric.Score => new[] { BreakdownAggregation.Distribution, BreakdownAggregation.Completion },
        // Pass is binary → stacked pass/fail or % passed.
        BreakdownMetric.Pass => new[] { BreakdownAggregation.Breakdown, BreakdownAggregation.Completion },
        // Age mirrors Score: a spread of days, or % of the folder recorded recently.
        BreakdownMetric.ChartAge => new[] { BreakdownAggregation.Distribution, BreakdownAggregation.Completion },
        // Grade / Plate are ordinal categories → all three (distribution = an average line).
        _ => new[]
        {
            BreakdownAggregation.Distribution, BreakdownAggregation.Breakdown, BreakdownAggregation.Completion
        }
    };
}
