using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Schedules an account for deletion. The account is hidden immediately and works normally
///     until the window elapses — invisible and scheduled, not half-locked.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RequestAccountDeletionCommand(Guid UserId) : IRequest<AccountDeletionResult>;
