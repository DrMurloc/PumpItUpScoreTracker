using MediatR;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Reads a player's captured milestones (gold rows) for specific sessions. Session-keyed
///     like <see cref="GetScoreHighlightsForSessionsQuery" />; session-less milestones (weekly
///     placements, admin recalcs) still read through the windowed query.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerMilestonesForSessionsQuery(Guid UserId, IReadOnlyCollection<Guid> SessionIds)
    : IQuery<IEnumerable<PlayerMilestoneRecord>>;
