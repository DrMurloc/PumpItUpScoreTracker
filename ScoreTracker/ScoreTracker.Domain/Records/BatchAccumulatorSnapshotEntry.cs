using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record BatchAccumulatorSnapshotEntry(
    Guid UserId,
    MixEnum Mix,
    DateTime FireAt,
    Guid[] NewChartIds,
    IReadOnlyDictionary<Guid, int> UpscoredChartIds);
