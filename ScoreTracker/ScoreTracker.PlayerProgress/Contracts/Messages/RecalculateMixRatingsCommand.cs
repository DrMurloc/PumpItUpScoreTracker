using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: recompute every player's stats and per-chart PUMBILITY for one mix
///     with the current formula. This is the exit path for formula adjustments — the
///     grade/plate/base constants live in one place (ScoringConfiguration); change them,
///     publish this, done. Published from the admin dashboard.
///     <para>
///         Rating gains found during the sweep mint the usual sessionless milestones and
///         stats-updated events (same as the per-user admin recalculation) — expect one
///         PUMBILITY-gain milestone per player whose total rose under a formula change.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecalculateMixRatingsCommand(MixEnum Mix)
{
}
