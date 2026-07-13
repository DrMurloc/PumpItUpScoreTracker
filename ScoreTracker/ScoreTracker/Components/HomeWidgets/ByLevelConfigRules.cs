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
            ? new[] { BreakdownMetric.LetterGrade, BreakdownMetric.Pass }
            : new[]
            {
                BreakdownMetric.Score, BreakdownMetric.LetterGrade, BreakdownMetric.Plate, BreakdownMetric.Pass
            };

    public static IReadOnlyList<BreakdownAggregation> AggregationsFor(BreakdownMetric metric) => metric switch
    {
        // Score is continuous → distribution or threshold completion (grades ARE its bands).
        BreakdownMetric.Score => new[] { BreakdownAggregation.Distribution, BreakdownAggregation.Completion },
        // Pass is binary → stacked pass/fail or % passed.
        BreakdownMetric.Pass => new[] { BreakdownAggregation.Breakdown, BreakdownAggregation.Completion },
        // Grade / Plate are ordinal categories → all three (distribution = an average line).
        _ => new[]
        {
            BreakdownAggregation.Distribution, BreakdownAggregation.Breakdown, BreakdownAggregation.Completion
        }
    };
}
