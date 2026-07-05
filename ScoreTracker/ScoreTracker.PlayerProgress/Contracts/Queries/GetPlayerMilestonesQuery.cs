using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>Windowed read of a player's captured milestones (gold rows on the Sessions page).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerMilestonesQuery(Guid UserId, MixEnum Mix, DateTimeOffset Since, DateTimeOffset Until)
    : IQuery<IEnumerable<PlayerMilestoneRecord>>;
