using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetUserByApiTokenQuery(Guid ApiToken) : IRequest<User?>
    {
    }
}
