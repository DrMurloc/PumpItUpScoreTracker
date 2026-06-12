using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record AdminSearchUsersQuery(string SearchText) : IQuery<IEnumerable<User>>
{
}
