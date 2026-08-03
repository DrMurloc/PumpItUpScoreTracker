namespace ScoreTracker.CommunityTools.Contracts.Messages;

/// <summary>
///     Re-attempts every delivery whose backoff has elapsed. Imperative trigger, published by the
///     recurring job — the queue lives in SQL, so a process death between attempts costs nothing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RetryDueWebhookDeliveriesCommand;

/// <summary>
///     Drops delivery bodies past their window and activity rows past theirs. Two horizons: a body
///     stops being useful once nobody can replay it, while the log itself stays readable longer.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PruneWebhookDeliveriesCommand;
