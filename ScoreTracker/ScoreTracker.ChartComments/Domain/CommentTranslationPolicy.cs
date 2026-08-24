namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     When an edit may re-queue a comment for translation. An edit while the text is still
///     waiting replaces the pending request free — the pipeline upserts by key and nothing has
///     been spent. Once a comment has been translated, an edit re-queues at most once per
///     24 hours: the spend ceiling is a fuse against bugs, and an edit loop is a user-driven cost
///     amplifier the fuse cannot see. The nightly batch already collapses same-day edits into one
///     translation; this is what holds when Drain now runs the submit off-schedule.
/// </summary>
internal static class CommentTranslationPolicy
{
    public static readonly TimeSpan RequeueCooldown = TimeSpan.FromHours(24);

    public static bool MayQueueAfterEdit(bool hadRenderings, DateTimeOffset? lastQueuedAt, DateTimeOffset now)
    {
        if (!hadRenderings || lastQueuedAt == null) return true;

        return now - lastQueuedAt.Value >= RequeueCooldown;
    }
}
