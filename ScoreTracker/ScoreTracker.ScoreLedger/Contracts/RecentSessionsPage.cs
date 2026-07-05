namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One page of a player's journal, grouped into sessions (or, for rows predating
///     session capture, calendar days), newest activity first.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecentSessionsPage(int TotalGroups, IReadOnlyList<RecentSessionsPage.SessionGroup> Groups)
{
    [ExcludeFromCodeCoverage]
    public sealed record SessionGroup(
        Guid? SessionId,
        DateOnly? Day,
        string Source,
        DateTimeOffset Start,
        DateTimeOffset End,
        IReadOnlyList<ScoreEventRecord> Rows);

    [ExcludeFromCodeCoverage]
    public sealed record ScoreEventRecord(
        Guid ChartId,
        DateTimeOffset OccurredAt,
        int? Score,
        string? Plate,
        bool IsBroken,
        string Source,
        Guid? SessionId,
        ScoreEventClassification Classification,
        int? PreviousBest);
}
