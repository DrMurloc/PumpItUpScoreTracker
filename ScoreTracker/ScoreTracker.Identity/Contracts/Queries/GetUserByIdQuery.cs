using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<User?>
{
}
