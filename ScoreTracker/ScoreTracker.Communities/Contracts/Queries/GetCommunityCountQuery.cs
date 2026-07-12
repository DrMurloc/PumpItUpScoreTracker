namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     How many player-made communities exist — regional (country) communities are
///     excluded, all privacy types count. Front-door stat (docs/design/front-door.md
///     D6); served from an in-process cache.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityCountQuery : IQuery<int>;
