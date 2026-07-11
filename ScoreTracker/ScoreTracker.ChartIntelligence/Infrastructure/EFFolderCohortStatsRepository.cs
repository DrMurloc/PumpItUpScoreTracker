using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFFolderCohortStatsRepository : IFolderCohortStatsRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFFolderCohortStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveFolder(MixEnum mix, ChartType chartType, int level,
        IEnumerable<FolderCohortBucketRecord> buckets, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var typeName = chartType.ToString();
        var existing = await database.Set<FolderCohortStatsEntity>()
            .Where(e => e.MixId == mixId && e.ChartType == typeName && e.Level == level)
            .ToArrayAsync(cancellationToken);
        database.Set<FolderCohortStatsEntity>().RemoveRange(existing);
        foreach (var bucket in buckets)
            await database.Set<FolderCohortStatsEntity>().AddAsync(new FolderCohortStatsEntity
            {
                MixId = mixId,
                ChartType = typeName,
                Level = level,
                Bucket = bucket.Bucket,
                PassHistogramJson = JsonSerializer.Serialize(bucket.PassHistogram)
            }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<FolderCohortBucketRecord>> GetBuckets(MixEnum mix, ChartType chartType, int level,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var typeName = chartType.ToString();
        return (await database.Set<FolderCohortStatsEntity>()
                .Where(e => e.MixId == mixId && e.ChartType == typeName && e.Level == level)
                .ToArrayAsync(cancellationToken))
            .Select(e => new FolderCohortBucketRecord(e.Bucket,
                JsonSerializer.Deserialize<Dictionary<int, int>>(e.PassHistogramJson) ?? new Dictionary<int, int>()));
    }
}
