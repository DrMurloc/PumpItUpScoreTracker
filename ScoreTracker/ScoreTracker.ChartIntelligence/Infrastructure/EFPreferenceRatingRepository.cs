using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFPreferenceRatingRepository : IChartPreferenceRepository
{

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPreferenceRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveRating(MixEnum mix, Guid userId, Guid chartId, PreferenceRating rating,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<UserPreferenceRatingEntity>()
            .Where(e => e.MixId == mixId && e.ChartId == chartId && e.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
            await database.Set<UserPreferenceRatingEntity>().AddAsync(new UserPreferenceRatingEntity
            {
                Id = Guid.NewGuid(),
                MixId = mixId,
                UserId = userId,
                ChartId = chartId,
                Rating = rating
            }, cancellationToken);
        else
            entity.Rating = rating;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAverageRating(MixEnum mix, Guid chartId, PreferenceRating averageRating, int ratingCount,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<ChartPreferenceRatingEntity>().Where(c => c.MixId == mixId && c.ChartId == chartId)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            await database.Set<ChartPreferenceRatingEntity>().AddAsync(new ChartPreferenceRatingEntity
            {
                Id = Guid.NewGuid(),
                MixId = mixId,
                ChartId = chartId,
                Rating = averageRating,
                Count = ratingCount
            }, cancellationToken);
        }
        else
        {
            entity.Rating = averageRating;
            entity.Count = ratingCount;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartPreferenceRatingRecord>> GetPreferenceRatings(MixEnum mix,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await database.Set<ChartPreferenceRatingEntity>().Where(c => c.MixId == mixId).Select(cpr =>
                new ChartPreferenceRatingRecord(cpr.ChartId, cpr.Rating, cpr.Count))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<PreferenceRating>> GetRatingsForChart(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return (await database.Set<UserPreferenceRatingEntity>().Where(e => e.MixId == mixId && e.ChartId == chartId)
            .ToArrayAsync(cancellationToken)).Select(e => PreferenceRating.From(e.Rating)).ToArray();
    }

    public async Task<IEnumerable<UserRatingsRecord>> GetUserRatings(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return (await database.Set<UserPreferenceRatingEntity>().Where(e => e.MixId == mixId && e.UserId == userId)
                .ToArrayAsync(cancellationToken))
            .Select(u => new UserRatingsRecord(u.ChartId, u.Rating))
            .ToArray();
    }
}
