using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Finds the account owning any of the given external ids for a provider (first match wins,
///     in the order given), backfills ids that aren't attached to anyone (identifier-drift
///     self-healing), and creates a fresh account when nothing matches. Ids owned by a
///     different account are never re-pointed — that collision surfaces as a merge invitation,
///     not a takeover.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ResolveExternalUserCommand(
    string LoginProviderName,
    IReadOnlyList<string> ExternalIds,
    Name DisplayName,
    Name? GameTag,
    Uri? ProfileImage) : IRequest<ExternalUserResolution>
{
}
