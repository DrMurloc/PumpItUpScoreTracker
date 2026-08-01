using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>One outbound delivery, as the queue and the console see it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record WebhookDeliveryRecord(
    Guid Id,
    Guid ToolId,
    Guid UserId,
    MixEnum Mix,
    WebhookMode Mode,
    string DeliveryId,
    string? Body,
    DateTimeOffset SignedAt,
    string? Signature,
    int Attempt,
    DeliveryStatus Status,
    int? RemoteStatusCode,
    WebhookFailureReason FailureReason,
    string? RemoteBodySnippet,
    int? LatencyMs,
    DateTimeOffset? NextAttemptAt,
    bool IsTest);

/// <summary>
///     How long a failure waits before the next try. Five attempts over roughly an hour, which is
///     long enough to ride out a deploy and short enough that a maker still finds the failure in
///     their log while they remember what they changed.
/// </summary>
internal static class WebhookRetry
{
    public const int MaxAttempts = 5;

    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(40)
    };

    /// <summary>When to try again after <paramref name="attempt" /> failures, or null when done.</summary>
    public static DateTimeOffset? NextAttemptAfter(int attempt, DateTimeOffset now)
    {
        return attempt >= MaxAttempts || attempt < 1 ? null : now + Backoff[Math.Min(attempt - 1, Backoff.Length - 1)];
    }
}

/// <summary>
///     What the body of a delivery is kept for, and therefore when it may be dropped.
///     <para>
///         A success nobody will replay does not need to exist; a failure does, so it can be retried
///         and replayed. A session-mode body is never written at all — it carries a live piugame.com
///         credential.
///     </para>
/// </summary>
internal static class WebhookRetention
{
    /// <summary>How long a failed body stays replayable.</summary>
    public static readonly TimeSpan Bodies = TimeSpan.FromDays(7);

    /// <summary>How long the activity log itself is kept.</summary>
    public static readonly TimeSpan Metadata = TimeSpan.FromDays(14);

    public static bool ShouldPersistBody(WebhookMode mode, DeliveryStatus status)
    {
        if (mode == WebhookMode.PiuGameSession) return false;

        return status is DeliveryStatus.Pending or DeliveryStatus.Failed or DeliveryStatus.Abandoned;
    }
}
