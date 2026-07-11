using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

internal interface IChartScoreStatsRepository
{
    Task SaveStats(MixEnum mix, IEnumerable<ChartScoreStatsRecord> stats, CancellationToken cancellationToken);

    Task<IEnumerable<ChartScoreStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken);
}

internal sealed record ChartScoreStatsRecord(Guid ChartId, double ScoreStandardDeviation, int ScoreCount);
