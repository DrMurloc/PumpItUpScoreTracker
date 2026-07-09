namespace ScoreTracker.PlayerProgress.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ScoreHighlightRecord(
    Guid ChartId,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    HighlightFlags Flags,
    int Level,
    double? ScoringLevel,
    HighlightDetail? Detail = null);
