using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Domain;

// Writes and the import-state stamp are keyed per mix; the read side stays mixless until
// the Phoenix 2 mirror semantics land (the P2 site replaced per-level rating boards with a
// single login-gated Pumbility board — only Phoenix rows exist until that work).
internal interface IOfficialLeaderboardRepository
{
    Task ClearLeaderboard(string leaderboardType, string leaderboardName, CancellationToken cancellationToken);

    Task WriteEntry(MixEnum mix, UserOfficialLeaderboard entry,
        CancellationToken cancellationToken);

    Task WriteEntries(MixEnum mix, IEnumerable<UserOfficialLeaderboard> entries,
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
    Task SaveWorldRanking(MixEnum mix, WorldRankingRecord record, CancellationToken cancellationToken);
    Task FixRankingOrders(CancellationToken cancellationToken);
    Task<IEnumerable<(string Username, Uri AvatarPath)>> GetUserAvatars(CancellationToken cancellationToken);
    Task UpdateAllAvatarPaths(Uri oldPath, Uri newPath, CancellationToken cancellationToken);
    Task SaveAvatar(string username, Uri avatarPath, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLastImportTimestamp(MixEnum mix, CancellationToken cancellationToken);
    Task SetLastImportTimestamp(MixEnum mix, DateTimeOffset timestamp, CancellationToken cancellationToken);
}