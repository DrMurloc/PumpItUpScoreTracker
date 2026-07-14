using ScoreTracker.Domain.Records;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     Registered players + distinct countries represented, for the public front door
///     (docs/design/front-door.md D6). Served from an in-process cache — safe to
///     dispatch on every anonymous landing-page render.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerbaseStatsQuery : IQuery<PlayerbaseCounts>;
