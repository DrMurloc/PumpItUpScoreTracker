using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>Calls off a scheduled deletion and restores what hiding the account changed.</summary>
[ExcludeFromCodeCoverage]
public sealed record CancelAccountDeletionCommand(Guid UserId) : IRequest;
