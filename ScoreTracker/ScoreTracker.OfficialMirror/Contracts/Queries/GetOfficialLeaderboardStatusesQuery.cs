using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Every official-leaderboard placement for one official-site username.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialLeaderboardStatusesQuery(string Username, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<UserOfficialLeaderboard>>
{
}
