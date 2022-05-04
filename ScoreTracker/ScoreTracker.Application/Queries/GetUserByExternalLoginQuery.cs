using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserByExternalLoginQuery(string ExternalId, string LoginProviderName) : IRequest<User?>
{
}