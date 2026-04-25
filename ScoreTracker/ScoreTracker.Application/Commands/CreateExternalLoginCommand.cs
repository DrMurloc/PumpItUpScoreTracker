using MediatR;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateExternalLoginCommand(Guid UserId, string ExternalId, string LoginProviderName) : IRequest
{
}
