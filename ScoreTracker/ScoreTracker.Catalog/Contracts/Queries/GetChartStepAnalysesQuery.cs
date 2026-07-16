using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Bulk sibling of <see cref="GetChartStepAnalysisQuery" /> for analytics sweeps (the
///     nightly similarity job reads every chart's scalars in one pass — flagged in
///     docs/design/chart-details-overhaul.md). Charts without banked analysis are absent
///     from the result; <see cref="ChartStepAnalysisRecord.ExternalKey" /> is deliberately
///     null on bulk results — link-outs are a per-chart display concern.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartStepAnalysesQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, ChartStepAnalysisRecord>>
{
}
