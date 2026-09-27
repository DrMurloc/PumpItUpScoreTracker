namespace ScoreTracker.Web.Services;

/// <summary>
///     The whole minutes a player waits before importing a mix again, for the refusal toast — rounded up,
///     so "1 min" never means "now" (docs/design/import-restart-recovery.md §0).
/// </summary>
public static class ImportCooldown
{
    public static int MinutesLeft(TimeSpan? retryAfter)
    {
        return Math.Max(1, (int)Math.Ceiling((retryAfter ?? TimeSpan.Zero).TotalMinutes));
    }
}
