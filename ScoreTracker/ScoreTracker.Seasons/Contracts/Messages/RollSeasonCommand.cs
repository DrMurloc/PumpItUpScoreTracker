namespace ScoreTracker.Seasons.Contracts.Messages;

/// <summary>
///     The daily roll (docs/design/seasons.md §7): open the quarter the clock stands in if it has no
///     row yet, and seal every season whose window has closed — there is no grace (D13), so the seal
///     is simply the next roll after the boundary. Stateless and idempotent — the 11:15 UTC job
///     publishes it, and so does Roll now on the admin console.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RollSeasonCommand
{
}
