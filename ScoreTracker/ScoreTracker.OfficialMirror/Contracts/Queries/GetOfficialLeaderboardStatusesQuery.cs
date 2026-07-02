using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Every official-leaderboard placement for one official-site username.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialLeaderboardStatusesQuery(string Username)
    : IQuery<IEnumerable<UserOfficialLeaderboard>>
{
}
