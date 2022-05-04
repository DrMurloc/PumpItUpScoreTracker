using MediatR;

namespace ScoreTracker.Application.Commands;

public sealed record CreateExternalLoginCommand(Guid UserId, string ExternalId, string LoginProviderName) : IRequest
{
}