using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFChartDifficultyRatingRepository : IChartDifficultyRatingRepository
{

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartDifficultyRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task RateChart(MixEnum mix, Guid chartId, Guid userId, DifficultyAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var scale = adjustment.GetScale();
        var existingRating = await database.Set<UserChartDifficultyRatingEntity>().FirstOrDefaultAsync(c =>
            c.ChartId == chartId && c.UserId == userId && c.MixId == mixId, cancellationToken);

        if (existingRating == null)
            await database.Set<UserChartDifficultyRatingEntity>().AddAsync(new UserChartDifficultyRatingEntity
            {
                ChartId = chartId,
                UserId = userId,
                Id = Guid.NewGuid(),
                MixId = mixId,
                Scale = scale
            }, cancellationToken);
        else
            existingRating.Scale = scale;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<DifficultyAdjustment>> GetRatings(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var scales = await database.Set<UserChartDifficultyRatingEntity>()
            .Where(u => u.ChartId == chartId && u.MixId == mixId).Select(u => u.Scale).ToArrayAsync(cancellationToken);

        return scales.Select(DifficultyAdjustmentHelpers.From).ToArray();
    }

    public async Task<IEnumerable<ChartDifficultyRatingRecord>> GetAllChartRatedDifficulties(MixEnum mix,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await database.Set<ChartDifficultyRatingEntity>()
            .Where(c => c.MixId == mixId)
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ChartDifficultyRatingRecord?> GetChartRatedDifficulty(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await database.Set<ChartDifficultyRatingEntity>().Where(c => c.ChartId == chartId && c.MixId == mixId)
            .Select(c => new ChartDifficultyRatingRecord(c.ChartId, c.Difficulty, c.Count, c.StandardDeviation))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ClearAdjustedDifficulty(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var entry = await database.Set<ChartDifficultyRatingEntity>().Where(c => c.ChartId == chartId && c.MixId == mixId)
            .FirstOrDefaultAsync(cancellationToken);
        if (entry != null)
        {
            database.Remove(entry);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DifficultyAdjustment?> GetRating(MixEnum mix, Guid chartId, Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var result = await database.Set<UserChartDifficultyRatingEntity>()
            .Where(c => c.ChartId == chartId && c.UserId == userId && c.MixId == mixId)
            .FirstOrDefaultAsync(cancellationToken);
        return result == null ? null : DifficultyAdjustmentHelpers.From(result.Scale);
    }

    public async Task<IEnumerable<(Guid ChartId, DifficultyAdjustment Rating)>> GetRatingsByUser(MixEnum mix,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var results = await database.Set<UserChartDifficultyRatingEntity>().Where(c => c.UserId == userId && c.MixId == mixId)
            .ToArrayAsync(cancellationToken);
        return results.Select(r => (r.ChartId, DifficultyAdjustmentHelpers.From(r.Scale))).ToArray();
    }

    public async Task<IEnumerable<CoOpRating>> GetAllCoOpRatings(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<CoOpRatingEntity>().ToArrayAsync(cancellationToken)).GroupBy(c => c.ChartId)
            .Select(g => new CoOpRating(g.Key, g.Count(g => g.Player == 1),
                g.ToDictionary(g => g.Player, g => (DifficultyLevel)g.Difficulty)))
            .ToArray();
    }

    public async Task<CoOpRating?> GetCoOpRating(Guid chartId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<CoOpRatingEntity>().Where(c => c.ChartId == chartId).ToArrayAsync(cancellationToken))
            .GroupBy(c => c.ChartId)
            .Select(g => new CoOpRating(g.Key, g.Count(g => g.Player == 1),
                g.ToDictionary(g => g.Player, g => (DifficultyLevel)g.Difficulty)))
            .FirstOrDefault();
    }

    public async Task SaveCoOpRating(CoOpRating rating, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var savedRatings = (await database.Set<CoOpRatingEntity>().Where(c => c.ChartId == rating.ChartId)
            .ToArrayAsync(cancellationToken));
        if (savedRatings.Any())
        {
            foreach (var r in savedRatings)
            {
                r.Difficulty = rating.Ratings[r.Player];
            }
        }
        else
        {
            var newEntities = rating.Ratings.Select(r => new CoOpRatingEntity
            {
                Id = Guid.NewGuid(),
                ChartId = rating.ChartId,
                Difficulty = r.Value,
                Player = r.Key
            });
            await database.Set<CoOpRatingEntity>().AddRangeAsync(newEntities, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearCoOpRating(Guid chartId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<CoOpRatingEntity>().Where(c => c.ChartId == chartId).ToArrayAsync(cancellationToken);
        database.Set<CoOpRatingEntity>().RemoveRange(entities);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDictionary<int, DifficultyLevel>?> GetMyCoOpRating(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<UserCoOpRatingEntity>().Where(ucr => ucr.ChartId == chartId && ucr.UserId == userId)
            .ToArrayAsync(cancellationToken);
        return entities.Any() ? entities.ToDictionary(e => e.Player, e => (DifficultyLevel)e.Difficulty) : null;
    }

    public async Task SetMyCoOpRating(Guid userId, Guid chartId, IDictionary<int, DifficultyLevel>? playerLevels,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<UserCoOpRatingEntity>().Where(ucr => ucr.ChartId == chartId && ucr.UserId == userId)
            .ToArrayAsync(cancellationToken);
        if (playerLevels == null)
        {
            database.Set<UserCoOpRatingEntity>().RemoveRange(entities);
        }
        else
        {
            if (entities.Any())
            {
                foreach (var e in entities)
                {
                    e.Difficulty = playerLevels[e.Player];
                }
            }
            else
            {
                var newEntities = playerLevels.Select(kv => new UserCoOpRatingEntity
                {
                    Id = Guid.NewGuid(),
                    ChartId = chartId,
                    UserId = userId,
                    Player = kv.Key,
                    Difficulty = kv.Value
                });
                await database.Set<UserCoOpRatingEntity>().AddRangeAsync(newEntities, cancellationToken);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDictionary<int, IEnumerable<DifficultyLevel>>> GetCoOpRatings(Guid chartId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<UserCoOpRatingEntity>().Where(c => c.ChartId == chartId).ToArrayAsync(cancellationToken))
            .GroupBy(c => c.Player).ToDictionary(kv => kv.Key,
                kv => kv.Select(l => (DifficultyLevel)l.Difficulty).ToArray().AsEnumerable());
    }

    public async Task SetAdjustedDifficulty(MixEnum mix, Guid chartId, double difficulty, int count,
        double standardDeviation,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var existing =
            await database.Set<ChartDifficultyRatingEntity>().FirstOrDefaultAsync(c => c.ChartId == chartId && c.MixId == mixId,
                cancellationToken);
        if (existing == null)
        {
            await database.Set<ChartDifficultyRatingEntity>().AddAsync(new ChartDifficultyRatingEntity
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

        await database.SaveChangesAsync(cancellationToken);
    }
}
