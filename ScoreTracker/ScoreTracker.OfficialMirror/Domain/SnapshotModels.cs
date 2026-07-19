namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One sweep run: run state while executing, a snapshot once CompletedAt seals it.
/// </summary>
internal sealed record SnapshotRun(int Id, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    bool IsBaseline, string Stage, int BoardsExpected, int BoardsWritten, int BoardsSkipped, string? Error);

internal sealed record BoardDimension(int Id, string LeaderboardType, string Name,
    Guid? ChartId, string? ChartType, int? Level);

internal sealed record PlayerDimension(int Id, string Username, Uri? Avatar, Guid? UserId);

internal sealed record PlacementRow(int LeaderboardId, int PlayerId, int Place, decimal Score);

internal sealed record BoardRecordRow(int LeaderboardId, int HighScore, int AchievedSnapshotId);

/// <summary>
///     Best-ever highs from every OTHER mix's record books — charts keyed by the
///     mix-agnostic ChartId, folders by their nominal (type, level). The cross-mix
///     reference that keeps a re-clear of a Phoenix-era achievement from reading as a
///     Phoenix 2 world first.
/// </summary>
internal sealed record CrossMixRecordHighs(
    IReadOnlyDictionary<Guid, int> ChartHighs,
    IReadOnlyDictionary<(string ChartType, int Level), int> FolderHighs)
{
    public static readonly CrossMixRecordHighs Empty = new(
        new Dictionary<Guid, int>(), new Dictionary<(string, int), int>());
}

internal sealed record FolderRecordRow(string ChartType, int Level, int HighScore, int AchievedSnapshotId);

internal sealed record HighlightRow(string Kind, int SortOrder, int PlayerId, int? DethronedPlayerId,
    int? LeaderboardId, Guid? ChartId, string? ChartType, int? Level, string? GradeBand,
    decimal? Score, decimal? PrevValue, decimal? NewValue);

internal sealed record RenameProposal(int Id, int OldPlayerId, int NewPlayerId, string OldUsername,
    string NewUsername, bool AvatarMatched, int Top50Overlap, string Status, int CreatedSnapshotId);

/// <summary>A placement joined with its board's dimension — the hub read shape.</summary>
internal sealed record PlacementDetail(int PlayerId, int LeaderboardId, string LeaderboardType, string BoardName,
    Guid? ChartId, string? ChartType, int? Level, int Place, decimal Score);

/// <summary>One row of a player's life across snapshots, sealed runs only.</summary>
internal sealed record PlayerTimelineRow(int SnapshotId, DateTimeOffset CompletedAt, string LeaderboardType,
    string BoardName, Guid? ChartId, int Place, decimal Score);

internal static class LeaderboardTypes
{
    public const string Rating = "Rating";
    public const string Chart = "Chart";
}

internal static class HighlightKinds
{
    public const string PumbilityMover = "PumbilityMover";
    public const string BoardsClimbed = "BoardsClimbed";
    public const string NewNumberOne = "NewNumberOne";
    public const string ChartGradeFirst = "ChartGradeFirst";
    public const string FolderGradeFirst = "FolderGradeFirst";
}

internal static class ProposalStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Dismissed = "Dismissed";
}
