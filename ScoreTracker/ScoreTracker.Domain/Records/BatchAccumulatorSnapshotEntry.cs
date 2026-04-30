namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record BatchAccumulatorSnapshotEntry(
    Guid UserId,
    DateTime FireAt,
    Guid[] NewChartIds,
    IReadOnlyDictionary<Guid, int> UpscoredChartIds);
