using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: patch ONLY the Phoenix 2 finale's projected <c>TotalPumbility</c> on every
///     stored recap — the targeted backfill for the 2026-07-13 aggregation fix (overall
///     PUMBILITY became a single merged top-50 instead of the two per-type pools summed).
///     Everything else in each payload — the Singles/Doubles projections, the projected
///     titles, and <c>ComputedAt</c> — rides through untouched, so it avoids the full-rebuild
///     sweep. Published from the admin dashboard. The finale projects Phoenix onto Phoenix 2,
///     so only Phoenix recaps carry a projection; other mixes are a no-op.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildRecapTotalPumbilityCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
