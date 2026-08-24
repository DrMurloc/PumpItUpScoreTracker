namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     How long a "Queued for translation" badge may keep promising. The stamp is written at
///     queue time and trusted after that, so every way the pipeline can silently lose a text — a
///     dropped in-memory message, a crash between complete and publish — would leave the badge
///     lying forever. Failures are announced and clear the stamp; this horizon is the backstop
///     for the losses nothing announces. Three days is generous: the pipeline turns a text
///     around in at most two nights.
///     <para>
///         The edit-requeue cooldown that used to live here moved to the pipeline's submit step
///         (owner-approved fix, 2026-08-24): blocking at the edit dropped renderings and then
///         never re-queued, stranding the comment — and was bypassed by editing twice anyway.
///         Edits always re-queue now; the once-per-24h wait happens where it cannot lose work.
///     </para>
/// </summary>
internal static class CommentTranslationPolicy
{
    public static readonly TimeSpan QueuedPromiseHorizon = TimeSpan.FromDays(3);

    public static bool PromiseStands(DateTimeOffset? translationQueuedAt, DateTimeOffset now)
    {
        return translationQueuedAt != null && now - translationQueuedAt.Value < QueuedPromiseHorizon;
    }
}
