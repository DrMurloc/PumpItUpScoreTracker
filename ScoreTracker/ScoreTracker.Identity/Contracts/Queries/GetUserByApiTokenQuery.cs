using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Identity.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetUserByApiTokenQuery(Guid ApiToken) : IQuery<User?>
    {
    }
}
