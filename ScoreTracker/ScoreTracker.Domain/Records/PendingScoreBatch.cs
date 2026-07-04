using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record PendingScoreBatch(MixEnum Mix, Guid[] NewChartIds, IDictionary<Guid, int> UpscoredChartIds)
{
}
