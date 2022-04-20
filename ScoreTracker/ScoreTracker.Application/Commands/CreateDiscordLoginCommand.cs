using MediatR;

namespace ScoreTracker.Application.Commands;

public sealed record CreateDiscordLoginCommand(Guid UserId, ulong DiscordId) : IRequest
{
}