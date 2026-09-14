namespace ScoreTracker.Seasons.Contracts.Messages;

/// <summary>
///     The one-shot backfill (docs/design/seasons.md D23, §7): create every quarter from Summer 2026
///     to the one running now, and ask each for its replay. Idempotent — a season that already exists
///     is left alone, and replaying a season's bests a second time lands on the same rows.
///     Never seals (D37): the ended quarters are stamped by the next roll.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillSeasonsCommand
{
}
