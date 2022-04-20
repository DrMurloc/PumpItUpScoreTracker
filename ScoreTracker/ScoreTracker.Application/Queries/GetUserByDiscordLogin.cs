using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserByDiscordLogin(ulong DiscordId) : IRequest<User?>
{
}