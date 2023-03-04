using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartDifficultyRatingRepository : IChartDifficultyRatingRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFChartDifficultyRatingRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task RateChart(Guid chartId, Guid userId, DifficultyAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        var scale = adjustment.GetScale();
        var existingRating = await _database.UserChartDifficultyRating.FirstOrDefaultAsync(c =>
            c.ChartId == chartId && c.UserId == userId, cancellationToken);

        if (existingRating == null)
            await _database.UserChartDifficultyRating.AddAsync(new UserChartDifficultyRatingEntity
            {
                ChartId = chartId,
                UserId = userId,
                Id = Guid.NewGuid(),
                Scale = scale
            }, cancellationToken);
        else
            existingRating.Scale = scale;

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DifficultyAdjustment>> GetRatings(Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var scales = await _database.UserChartDifficultyRating
            .Where(u => u.ChartId == chartId).Select(u => u.Scale).ToArrayAsync(cancellationToken);

        return scales.Select(DifficultyAdjustmentHelpers.From).ToArray();
    }

    public async Task<IEnumerable<ChartDifficultyRatingRecord>> GetAllChartRatedDifficulties(
        CancellationToken cancellationToken = default)
    {
        return await _database.ChartDifficultyRating
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ChartDifficultyRatingRecord?> GetChartRatedDifficulty(Guid chartId,
        CancellationToken cancellationToken = default)
    {
        return await _database.ChartDifficultyRating.Where(c => c.ChartId == chartId)
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DifficultyAdjustment?> GetRating(Guid chartId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await _database.UserChartDifficultyRating.Where(c => c.ChartId == chartId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return result == null ? null : DifficultyAdjustmentHelpers.From(result.Scale);
    }

    public async Task<IEnumerable<(Guid ChartId, DifficultyAdjustment Rating)>> GetRatingsByUser(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var results = await _database.UserChartDifficultyRating.Where(c => c.UserId == userId)
            .ToArrayAsync(cancellationToken);
        return results.Select(r => (r.ChartId, DifficultyAdjustmentHelpers.From(r.Scale))).ToArray();
    }

    public async Task SetAdjustedDifficulty(Guid chartId, double difficulty, int count, double standardDeviation,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await _database.ChartDifficultyRating.FirstOrDefaultAsync(c => c.ChartId == chartId, cancellationToken);
        if (existing == null)
        {
            await _database.ChartDifficultyRating.AddAsync(new ChartDifficultyRatingEntity
            {
                ChartId = chartId,
                Count = count,
                Difficulty = difficulty,
                StandardDeviation = standardDeviation
            }, cancellationToken);
        }
        else
        {
            existing.Difficulty = difficulty;
            existing.Count = count;
            existing.StandardDeviation = standardDeviation;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }
}