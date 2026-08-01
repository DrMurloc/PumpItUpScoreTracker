using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     The delivery queue, the console's data, and the pull-based event feed — one table serving all
///     three, which is why durability is nearly free here.
/// </summary>
internal interface IWebhookDeliveryRepository
{
    /// <summary>
    ///     Records a delivery before it is attempted. Writing first is what makes the queue survive
    ///     a process death: the bus is in-memory, so an in-flight delivery would otherwise vanish
    ///     with no trace and no retry.
    /// </summary>
    Task<Guid> Enqueue(Guid toolId, Guid userId, MixEnum mix, WebhookMode mode, string deliveryId,
        string? body, string? signature, DateTimeOffset signedAt, bool isTest,
        CancellationToken cancellationToken = default);

    Task RecordSuccess(Guid id, int statusCode, int latencyMs, bool keepBody,
        CancellationToken cancellationToken = default);

    Task RecordFailure(Guid id, int attempt, WebhookFailureReason reason, int? statusCode,
        string? remoteBodySnippet, int? latencyMs, DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDeliveryRecord>> GetDue(DateTimeOffset now, int limit,
        CancellationToken cancellationToken = default);

    Task<WebhookDeliveryRecord?> Get(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookDeliveryRecord>> GetForTool(Guid toolId, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent delivery whose body we still hold — the console's signature sample.</summary>
    Task<WebhookDeliveryRecord?> GetLatestWithBody(Guid toolId, CancellationToken cancellationToken = default);

    /// <summary>Drops bodies past their window and rows past theirs. Two horizons, one sweep.</summary>
    Task Prune(DateTimeOffset bodiesBefore, DateTimeOffset rowsBefore,
        CancellationToken cancellationToken = default);
}
