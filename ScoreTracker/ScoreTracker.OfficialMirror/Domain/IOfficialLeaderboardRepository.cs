using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Domain;

// Reads AND writes are keyed per mix: Phoenix and Phoenix 2 boards share names ("Conflict
// S22" exists on both sites), so a mixless clear or read would cross the mirrors.
internal interface IOfficialLeaderboardRepository
{
    Task ClearLeaderboard(MixEnum mix, string leaderboardType, string leaderboardName,
        CancellationToken cancellationToken);

    Task WriteEntry(MixEnum mix, UserOfficialLeaderboard entry,
        CancellationToken cancellationToken);

    Task WriteEntries(MixEnum mix, IEnumerable<UserOfficialLeaderboard> entries,
        CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(MixEnum mix, string? leaderboardType,
        CancellationToken cancellationToken);

    Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(MixEnum mix, CancellationToken cancellationToken)
    {
        return GetOfficialLeaderboardUsernames(mix, null, cancellationToken);
    }

    Task<IEnumerable<UserOfficialLeaderboard>> GetOfficialLeaderboardStatuses(MixEnum mix, string username,
        CancellationToken cancellationToken);

    Task<IEnumerable<WorldRankingRecord>> GetAllWorldRankings(MixEnum mix, CancellationToken cancellationToken);
    Task DeleteWorldRankings(MixEnum mix, CancellationToken cancellationToken);
    Task SaveWorldRanking(MixEnum mix, WorldRankingRecord record, CancellationToken cancellationToken);
    Task FixRankingOrders(MixEnum mix, CancellationToken cancellationToken);
    Task<IEnumerable<(string Username, Uri AvatarPath)>> GetUserAvatars(CancellationToken cancellationToken);
    Task UpdateAllAvatarPaths(Uri oldPath, Uri newPath, CancellationToken cancellationToken);
    Task SaveAvatar(string username, Uri avatarPath, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLastImportTimestamp(MixEnum mix, CancellationToken cancellationToken);
    Task SetLastImportTimestamp(MixEnum mix, DateTimeOffset timestamp, CancellationToken cancellationToken);
}