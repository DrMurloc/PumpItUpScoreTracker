using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     The delivery queue and the console's data — one table serving both, which is why durability is
///     nearly free here.
/// </summary>
internal interface IWebhookDeliveryRepository
{
    /// <summary>
    ///     Records a delivery before it is attempted. Writing first is what makes the queue survive
    ///     a process death: the bus is in-memory, so an in-flight delivery would otherwise vanish
    ///     with no trace and no retry.
    /// </summary>
    Task<Guid> Enqueue(Guid toolId, Guid userId, MixEnum mix, WebhookMode mode, string deliveryId,
        string? body, DateTimeOffset queuedAt, bool isTest,
        CancellationToken cancellationToken = default);

    Task RecordSuccess(Guid id, int attempt, int statusCode, int latencyMs, bool keepBody,
        CancellationToken cancellationToken = default);

    Task RecordFailure(Guid id, int attempt, WebhookFailureReason reason, int? statusCode,
        string? remoteBodySnippet, int? latencyMs, DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Takes the deliveries whose backoff has elapsed <b>and claims them</b> — the same call
    ///     pushes their next attempt past the claim window, so a sweep that overlaps the previous one
    ///     finds nothing rather than re-POSTing the same rows.
    ///     <para>
    ///         The overlap is the common case, not the rare one: the queue only fills when endpoints
    ///         are down, and a sweep of dead endpoints runs at ten seconds each. Without a claim, 200
    ///         dead rows means a half-hour sweep with six more starting on top of it.
    ///     </para>
    /// </summary>
    Task<IReadOnlyList<WebhookDeliveryRecord>> GetDue(DateTimeOffset now, int limit,
        DateTimeOffset claimUntil, CancellationToken cancellationToken = default);

    Task<WebhookDeliveryRecord?> Get(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDeliveryRecord>> GetForTool(Guid toolId, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent delivery whose body we still hold — the console's sample.</summary>
    Task<WebhookDeliveryRecord?> GetLatestWithBody(Guid toolId, CancellationToken cancellationToken = default);

    /// <summary>Drops bodies past their window and rows past theirs. Two horizons, one sweep.</summary>
    Task Prune(DateTimeOffset bodiesBefore, DateTimeOffset rowsBefore,
        CancellationToken cancellationToken = default);
}
