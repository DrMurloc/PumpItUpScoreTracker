using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetUserByApiTokenQuery(Guid ApiToken) : IRequest<User?>
    {
    }
}
