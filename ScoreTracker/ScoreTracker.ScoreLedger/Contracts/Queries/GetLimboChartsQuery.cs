using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The charts carrying a limbo leaderboard in this mix (docs/design/limbo-leaderboard.md D1).
///     Answered from a short-lived cache, because every chart view asks it to decide whether the
///     Lowest Passing chip renders at all.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLimboChartsQuery(MixEnum Mix) : IQuery<IReadOnlySet<Guid>>
{
}
