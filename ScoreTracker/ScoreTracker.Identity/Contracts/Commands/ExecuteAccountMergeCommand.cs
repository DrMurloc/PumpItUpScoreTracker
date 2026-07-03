using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Merges two accounts: every sign-in method of the retired account re-points to the
///     survivor, the retired account is hidden (not deleted — data purges after the grace
///     window, see AccountPurgeStartedEvent) and its sessions are invalidated. The caller is
///     responsible for having proven control of both accounts (login-overhaul design: one
///     successful sign-in per account); the handler only verifies the current user is one of
///     the two.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ExecuteAccountMergeCommand(Guid SurvivorUserId, Guid RetiredUserId)
    : IRequest<AccountMergeRecord>
{
}
