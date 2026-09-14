namespace ScoreTracker.Seasons.Contracts.Messages;

/// <summary>
///     The daily roll (docs/design/seasons.md §7): open the quarter the clock stands in if it has no
///     row yet, and seal every ended season past its seven-day grace. Stateless and idempotent — the
///     11:15 UTC job publishes it, and so does Roll now on the admin console.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RollSeasonCommand
{
}
