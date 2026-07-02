using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Identity.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record AdminSearchUsersQuery(string SearchText) : IQuery<IEnumerable<User>>
{
}
