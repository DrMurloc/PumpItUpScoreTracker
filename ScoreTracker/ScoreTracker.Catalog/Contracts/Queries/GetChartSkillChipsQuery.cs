using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Bulk read for the tier-list cards: each chart's skills ordered by dominance
///     (their top-3 pick first, then by segment coverage). Charts without banked
///     analysis are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartSkillChipsQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>>
{
}
