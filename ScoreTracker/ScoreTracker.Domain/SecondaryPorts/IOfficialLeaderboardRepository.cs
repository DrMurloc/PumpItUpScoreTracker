using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IOfficialLeaderboardRepository
{
    Task ClearLeaderboard(string leaderboardType, string leaderboardName, CancellationToken cancellationToken);

    Task WriteEntry(UserOfficialLeaderboard entry,
        CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(string? leaderboardType,
        CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(CancellationToken cancellationToken)
    {
        return GetOfficialLeaderboardUsernames(null, cancellationToken);
    }

    Task<IEnumerable<UserOfficialLeaderboard>> GetOfficialLeaderboardStatuses(string username,
        CancellationToken cancellationToken);

    Task<IEnumerable<WorldRankingRecord>> GetAllWorldRankings(CancellationToken cancellationToken);
    Task DeleteWorldRankings(CancellationToken cancellationToken);
    Task SaveWorldRanking(WorldRankingRecord record, CancellationToken cancellationToken);
    Task FixRankingOrders(CancellationToken cancellationToken);
    Task<IEnumerable<(string Username, Uri AvatarPath)>> GetUserAvatars(CancellationToken cancellationToken);
    Task UpdateAllAvatarPaths(Uri oldPath, Uri newPath, CancellationToken cancellationToken);
    Task SaveAvatar(string username, Uri avatarPath, CancellationToken cancellationToken);
}