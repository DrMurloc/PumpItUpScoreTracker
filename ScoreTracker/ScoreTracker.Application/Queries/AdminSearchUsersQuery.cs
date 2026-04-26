using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record AdminSearchUsersQuery(string SearchText) : IRequest<IEnumerable<User>>
{
}
