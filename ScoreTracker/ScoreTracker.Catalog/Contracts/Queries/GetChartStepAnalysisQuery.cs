using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>Null when piucenter has no banked analysis for the chart.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartStepAnalysisQuery(Guid ChartId) : IQuery<ChartStepAnalysisRecord?>
{
}
