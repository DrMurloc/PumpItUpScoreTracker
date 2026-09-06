using ScoreTracker.Domain.Models;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     <see cref="GetUserByIdQuery" /> for a set of ids at once — one read for a page of rows
///     that each name a player, where a query per row would put a hundred round trips behind
///     one API response. Ids that resolve to no user are simply absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUsersByIdsQuery(IReadOnlyCollection<Guid> UserIds) : IQuery<IReadOnlyList<User>>;
