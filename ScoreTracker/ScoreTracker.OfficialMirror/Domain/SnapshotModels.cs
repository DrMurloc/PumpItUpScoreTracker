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

internal sealed record FolderRecordRow(string ChartType, int Level, int HighScore, int AchievedSnapshotId);

internal sealed record HighlightRow(string Kind, int SortOrder, int PlayerId, int? DethronedPlayerId,
    int? LeaderboardId, Guid? ChartId, string? ChartType, int? Level, string? GradeBand,
    decimal? Score, decimal? PrevValue, decimal? NewValue);

internal sealed record RenameProposal(int Id, int OldPlayerId, int NewPlayerId, string OldUsername,
    string NewUsername, bool AvatarMatched, int Top50Overlap, string Status, int CreatedSnapshotId);

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
