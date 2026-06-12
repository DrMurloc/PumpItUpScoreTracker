using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<User?>
{
}
