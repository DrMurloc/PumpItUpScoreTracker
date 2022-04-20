using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserByDiscordLoginQuery(ulong DiscordId) : IRequest<User?>
{
}