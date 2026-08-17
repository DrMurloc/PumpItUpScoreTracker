using ScoreTracker.Domain.Records;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     How the caller may see each of these players — the same bases the player search stamps on
///     its hits, for rows that arrived by another route (a board player whose tag is linked to an
///     account). Ids that resolve to no player are simply absent from the answer.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayersVisibilityQuery(IReadOnlyCollection<Guid> UserIds)
    : IQuery<IReadOnlyDictionary<Guid, PlayerVisibility>>;
