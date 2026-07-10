using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: patch ONLY the ImpressivePgs field on every stored recap for the mix —
///     a targeted backfill for PG-card logic changes that leaves the rest of each payload
///     (and its ComputedAt) untouched, instead of the full-rebuild sweep. Published from
///     the admin dashboard.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildRecapPgCardsCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
