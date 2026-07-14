using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The per-grade percentile curves the nightly letter-difficulty rebuild computes
///     (this vertical's data — published so the chart page stops reaching for the
///     repository port). Percentiles are 0–100 as persisted; charts without rows are
///     absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartLetterDifficultiesQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParagonLevel, double>>>
{
}
