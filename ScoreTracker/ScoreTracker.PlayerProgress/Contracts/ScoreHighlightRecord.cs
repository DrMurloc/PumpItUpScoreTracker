namespace ScoreTracker.PlayerProgress.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ScoreHighlightRecord(
    Guid ChartId,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    HighlightFlag Flags,
    int Level,
    double? ScoringLevel);
