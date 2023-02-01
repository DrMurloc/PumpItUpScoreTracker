using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartDifficultyRatingRepository
{
    Task RateChart(Guid chartId, Guid userId, DifficultyAdjustment adjustment,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DifficultyAdjustment>> GetRatings(Guid chartId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartDifficultyRatingRecord>> GetAllChartRatedDifficulties(
        CancellationToken cancellationToken = default);

    Task<ChartDifficultyRatingRecord?> GetChartRatedDifficulty(Guid chartId,
        CancellationToken cancellationToken = default);

    Task SetAdjustedDifficulty(Guid chartId, double difficulty, int count,
        CancellationToken cancellationToken = default);

    Task<DifficultyAdjustment?> GetRating(Guid chartId, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<(Guid ChartId, DifficultyAdjustment Rating)>> GetRatingsByUser(Guid userId,
        CancellationToken cancellationToken = default);
}