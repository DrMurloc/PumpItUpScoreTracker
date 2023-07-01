using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartDifficultyRatingRepository : IChartDifficultyRatingRepository
{
    //Will  need to refactor this if I ever support non prod environments
    //Mostly saving some tedious joins for now.
    private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
    {
        { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
        { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
    };

    private readonly ChartAttemptDbContext _database;

    public EFChartDifficultyRatingRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task RateChart(MixEnum mix, Guid chartId, Guid userId, DifficultyAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var scale = adjustment.GetScale();
        var existingRating = await _database.UserChartDifficultyRating.FirstOrDefaultAsync(c =>
            c.ChartId == chartId && c.UserId == userId && c.MixId == mixId, cancellationToken);

        if (existingRating == null)
            await _database.UserChartDifficultyRating.AddAsync(new UserChartDifficultyRatingEntity
            {
                ChartId = chartId,
                UserId = userId,
                Id = Guid.NewGuid(),
                MixId = mixId,
                Scale = scale
            }, cancellationToken);
        else
            existingRating.Scale = scale;

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DifficultyAdjustment>> GetRatings(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var scales = await _database.UserChartDifficultyRating
            .Where(u => u.ChartId == chartId && u.MixId == mixId).Select(u => u.Scale).ToArrayAsync(cancellationToken);

        return scales.Select(DifficultyAdjustmentHelpers.From).ToArray();
    }

    public async Task<IEnumerable<ChartDifficultyRatingRecord>> GetAllChartRatedDifficulties(MixEnum mix,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        return await _database.ChartDifficultyRating
            .Where(c => c.MixId == mixId)
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ChartDifficultyRatingRecord?> GetChartRatedDifficulty(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        return await _database.ChartDifficultyRating.Where(c => c.ChartId == chartId && c.MixId == mixId)
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DifficultyAdjustment?> GetRating(MixEnum mix, Guid chartId, Guid userId,
        CancellationToken cancellationToken)
    {
        var mixId = MixGuids[mix];
        var result = await _database.UserChartDifficultyRating
            .Where(c => c.ChartId == chartId && c.UserId == userId && c.MixId == mixId)
            .FirstOrDefaultAsync(cancellationToken);
        return result == null ? null : DifficultyAdjustmentHelpers.From(result.Scale);
    }

    public async Task<IEnumerable<(Guid ChartId, DifficultyAdjustment Rating)>> GetRatingsByUser(MixEnum mix,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var results = await _database.UserChartDifficultyRating.Where(c => c.UserId == userId && c.MixId == mixId)
            .ToArrayAsync(cancellationToken);
        return results.Select(r => (r.ChartId, DifficultyAdjustmentHelpers.From(r.Scale))).ToArray();
    }

    public async Task SetAdjustedDifficulty(MixEnum mix, Guid chartId, double difficulty, int count,
        double standardDeviation,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var existing =
            await _database.ChartDifficultyRating.FirstOrDefaultAsync(c => c.ChartId == chartId && c.MixId == mixId,
                cancellationToken);
        if (existing == null)
        {
            await _database.ChartDifficultyRating.AddAsync(new ChartDifficultyRatingEntity
            {
                ChartId = chartId,
                Count = count,
                Difficulty = difficulty,
                StandardDeviation = standardDeviation,
                MixId = mixId
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