using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Infrastructure;

internal sealed class EFExternalChartAliasRepository : IExternalChartAliasRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFExternalChartAliasRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveAliases(string source, IEnumerable<ExternalChartAlias> aliases,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<ExternalChartAliasEntity>()
            .Where(e => e.Source == source)
            .ToDictionaryAsync(e => e.ExternalKey, cancellationToken);

        foreach (var alias in aliases)
            if (existing.TryGetValue(alias.ExternalKey, out var entity))
            {
                entity.ChartId = alias.ChartId;
                entity.Status = alias.Status.ToString();
                entity.LastCheckedAt = alias.LastCheckedAt;
            }
            else
            {
                await database.Set<ExternalChartAliasEntity>().AddAsync(new ExternalChartAliasEntity
                {
                    Id = Guid.NewGuid(),
                    Source = source,
                    ExternalKey = alias.ExternalKey,
                    ChartId = alias.ChartId,
                    Status = alias.Status.ToString(),
                    LastCheckedAt = alias.LastCheckedAt
                }, cancellationToken);
            }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalChartAlias>> GetAliases(string source,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ExternalChartAliasEntity>()
                .Where(e => e.Source == source)
                .ToArrayAsync(cancellationToken))
            .Select(e => new ExternalChartAlias(e.ExternalKey, e.ChartId,
                Enum.Parse<ExternalAliasStatus>(e.Status), e.LastCheckedAt))
            .ToArray();
    }

    public async Task ResolveAlias(string source, string externalKey, Guid chartId, DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ExternalChartAliasEntity>()
            .SingleOrDefaultAsync(e => e.Source == source && e.ExternalKey == externalKey, cancellationToken);
        if (entity == null)
        {
            entity = new ExternalChartAliasEntity
            {
                Id = Guid.NewGuid(),
                Source = source,
                ExternalKey = externalKey
            };
            await database.Set<ExternalChartAliasEntity>().AddAsync(entity, cancellationToken);
        }

        entity.ChartId = chartId;
        entity.Status = ExternalAliasStatus.Manual.ToString();
        entity.LastCheckedAt = resolvedAt;
        await database.SaveChangesAsync(cancellationToken);
    }
}
