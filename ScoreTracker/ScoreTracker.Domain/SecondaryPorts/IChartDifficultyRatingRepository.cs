using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartDifficultyRatingRepository
{
    Task RateChart(MixEnum mix, Guid chartId, Guid userId, DifficultyAdjustment adjustment,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DifficultyAdjustment>> GetRatings(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartDifficultyRatingRecord>> GetAllChartRatedDifficulties(MixEnum mix,
        CancellationToken cancellationToken = default);

    Task<ChartDifficultyRatingRecord?> GetChartRatedDifficulty(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default);

    Task SetAdjustedDifficulty(MixEnum mix, Guid chartId, double difficulty, int count, double standardDeviation,
        CancellationToken cancellationToken = default);

    Task<DifficultyAdjustment?> GetRating(MixEnum mix, Guid chartId, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<(Guid ChartId, DifficultyAdjustment Rating)>> GetRatingsByUser(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default);
}