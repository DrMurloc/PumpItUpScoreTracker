using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateExternalLoginCommand(Guid UserId, string ExternalId, string LoginProviderName) : IRequest
{
}
