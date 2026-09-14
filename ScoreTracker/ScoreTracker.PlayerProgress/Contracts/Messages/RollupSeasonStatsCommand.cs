using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Recompute every player's season stats row from their seasonal bests
///     (docs/design/seasons.md §7). Nightly, so a season's board never depends on an import having
///     fired the in-process pass — and once per quarter behind the backfill, which produces the bests
///     without ever running that pass.
/// </summary>
/// <param name="Season">
///     One season, or null for every season still open — the running one, and an ended one still
///     inside its seven-day grace. A sealed season is never recomputed (D13).
/// </param>
[ExcludeFromCodeCoverage]
public sealed record RollupSeasonStatsCommand(SeasonId? Season = null)
{
}
