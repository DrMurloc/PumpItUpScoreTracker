using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Attaches a set of external-id aliases for one provider to the current account. If any
///     alias already belongs to a different account, nothing is linked and ConflictingAccount
///     is returned — the caller surfaces that as a merge invitation, never a takeover.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LinkExternalAliasesCommand(string LoginProviderName, IReadOnlyList<string> ExternalIds)
    : IRequest<ExternalLinkOutcome>
{
}
