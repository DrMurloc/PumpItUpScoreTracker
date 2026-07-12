using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFUserTierListRepository : IUserTierListRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFUserTierListRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveUserFolder(MixEnum mix, Guid userId, IReadOnlyCollection<Guid> folderChartIds,
        IEnumerable<SongTierListEntry> entries, IReadOnlyDictionary<Guid, double> freshnessByChart,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        // Stray entries outside the folder would violate the replace-the-folder contract.
        var entryByChart = entries.Where(e => folderChartIds.Contains(e.ChartId))
            .ToDictionary(e => e.ChartId);
        var existing = await database.Set<UserTierListEntryEntity>()
            .Where(e => e.MixId == mixId && e.UserId == userId && folderChartIds.Contains(e.ChartId))
            .ToArrayAsync(cancellationToken);

        foreach (var entity in existing)
            if (entryByChart.TryGetValue(entity.ChartId, out var entry))
            {
                entity.Category = entry.Category.ToString();
                entity.Order = entry.Order;
                entity.Freshness = freshnessByChart.GetValueOrDefault(entity.ChartId, 1.0);
                entryByChart.Remove(entity.ChartId);
            }
            else
            {
                database.Set<UserTierListEntryEntity>().Remove(entity);
            }

        foreach (var entry in entryByChart.Values)
            await database.Set<UserTierListEntryEntity>().AddAsync(new UserTierListEntryEntity
            {
                MixId = mixId,
                UserId = userId,
                ChartId = entry.ChartId,
                Category = entry.Category.ToString(),
                Order = entry.Order,
                Freshness = freshnessByChart.GetValueOrDefault(entry.ChartId, 1.0)
            }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserTierListEntryRecord>> GetEntriesForCharts(MixEnum mix,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var ids = chartIds.Distinct().ToArray();
        return (await database.Set<UserTierListEntryEntity>()
                .Where(e => e.MixId == mixId && ids.Contains(e.ChartId))
                .ToArrayAsync(cancellationToken))
            .Select(e => new UserTierListEntryRecord(e.UserId, e.ChartId,
                Enum.Parse<TierListCategory>(e.Category), e.Order, e.Freshness));
    }
}
