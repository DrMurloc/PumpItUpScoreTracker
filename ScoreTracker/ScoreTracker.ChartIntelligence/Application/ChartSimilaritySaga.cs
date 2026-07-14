using MassTransit;
using ScoreTracker.ChartIntelligence.Contracts.Messages;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The similarity-graph saga (docs/design/chart-similarity.md). B1 wires the message,
///     schedule, table, and registration end-to-end; B2 fills the computation — bucket the
///     mix's charts by (type, level ±2), build the feature vectors, run the calculator, and
///     ReplaceEdges per chart — and adds GetSimilarChartsQuery with this class as handler.
/// </summary>
internal sealed class ChartSimilaritySaga : IConsumer<RecalculateChartSimilarityCommand>
{
    public Task Consume(ConsumeContext<RecalculateChartSimilarityCommand> context)
    {
        return Task.CompletedTask;
    }
}
