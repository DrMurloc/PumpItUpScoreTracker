using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IOfficialLeaderboardRepository
{
    Task ClearLeaderboard(string leaderboardType, string leaderboardName, CancellationToken cancellationToken);

    Task WriteEntry(UserOfficialLeaderboard entry,
        CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetOfficialLeaderboardStatuses(string username,
        CancellationToken cancellationToken);
}