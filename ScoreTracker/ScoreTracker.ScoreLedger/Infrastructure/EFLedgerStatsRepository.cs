using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFLedgerStatsRepository : ILedgerStatsRepository
{
    // Both reads serve the anonymous front door, so they cache in-process (the
    // EFChartRepository precedent). Six hours keeps the pulse's newest bar honest
    // enough while drive-by traffic costs zero DB.
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
    private const string TotalsCacheKey = $"{nameof(EFLedgerStatsRepository)}_Totals";
    private const string DailyCacheKeyPrefix = $"{nameof(EFLedgerStatsRepository)}_Daily_";

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFLedgerStatsRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public async Task<LedgerTotals> GetTotals(CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(TotalsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return new LedgerTotals(
                await database.Set<PhoenixRecordEntity>().LongCountAsync(cancellationToken),
                await database.Set<BestAttemptEntity>().LongCountAsync(cancellationToken));
        }))!;
    }

    public async Task<IReadOnlyList<LedgerDayVolume>> GetDailyVolumes(DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        // The window start is part of the key; it advances once a day, so stale keys
        // just age out with the TTL.
        var cacheKey = DailyCacheKeyPrefix + sinceUtc.UtcDateTime.ToString("yyyyMMdd");
        return (await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            // Backfill rows are seeded history dated at each record's last update —
            // not activity, so they never count toward the pulse.
            var buckets = await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.OccurredAt >= sinceUtc && e.Source != ScoreJournalEntry.BackfillSource)
                .GroupBy(e => e.OccurredAt.Date)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToArrayAsync(cancellationToken);
            return (IReadOnlyList<LedgerDayVolume>)buckets
                .Select(b => new LedgerDayVolume(DateOnly.FromDateTime(b.Key), b.Count))
                .OrderBy(v => v.Day)
                .ToArray();
        }))!;
    }
}
