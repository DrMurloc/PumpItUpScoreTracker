using MediatR;

namespace ScoreTracker.Translations.Contracts.Commands;

/// <summary>
///     Re-queues every failed text — the admin's lever beside Drain now, because Failed must not
///     be a dead end whose only exit is the author happening to edit. Admin-only, enforced in the
///     handler. The re-queued texts spend against the ceiling on the coming nights like any other
///     pending work. Returns how many were re-queued.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RetryFailedTranslationsCommand : IRequest<int>;
