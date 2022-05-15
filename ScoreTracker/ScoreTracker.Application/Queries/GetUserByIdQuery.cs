using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<User?>
{
}