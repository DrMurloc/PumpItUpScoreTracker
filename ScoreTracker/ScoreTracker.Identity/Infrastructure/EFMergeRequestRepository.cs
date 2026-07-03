using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure.Entities;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class EFMergeRequestRepository : IMergeRequestRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFMergeRequestRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(MergeRequest merge, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var set = database.Set<MergeRequestEntity>();
        var entity = await set.SingleOrDefaultAsync(m => m.Id == merge.Id, cancellationToken);
        if (entity == null)
        {
            entity = new MergeRequestEntity { Id = merge.Id };
            await set.AddAsync(entity, cancellationToken);
        }

        entity.SurvivorUserId = merge.SurvivorUserId;
        entity.RetiredUserId = merge.RetiredUserId;
        entity.MovedLogins = JsonSerializer.Serialize(merge.MovedLogins);
        entity.RetiredUserSnapshot = JsonSerializer.Serialize(merge.Snapshot);
        entity.State = merge.State.ToString();
        entity.CreatedAt = merge.CreatedAt;
        entity.PurgeAfter = merge.PurgeAfter;
        entity.PurgedAt = merge.PurgedAt;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<MergeRequest?> Get(Guid id, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MergeRequestEntity>()
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        return entity == null ? null : ToModel(entity);
    }

    public async Task<IEnumerable<MergeRequest>> GetActiveInvolving(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var active = MergeState.Active.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MergeRequestEntity>()
                .Where(m => m.State == active && (m.SurvivorUserId == userId || m.RetiredUserId == userId))
                .ToArrayAsync(cancellationToken))
            .Select(ToModel)
            .ToArray();
    }

    public async Task<IEnumerable<MergeRequest>> GetPurgeable(DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var purgeableStates = new[] { MergeState.Active.ToString(), MergeState.Purging.ToString() };
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MergeRequestEntity>()
                .Where(m => purgeableStates.Contains(m.State) && m.PurgeAfter <= asOf)
                .ToArrayAsync(cancellationToken))
            .Select(ToModel)
            .ToArray();
    }

    private static MergeRequest ToModel(MergeRequestEntity entity)
    {
        return new MergeRequest(entity.Id, entity.SurvivorUserId, entity.RetiredUserId,
            JsonSerializer.Deserialize<ExternalLoginRecord[]>(entity.MovedLogins) ??
            Array.Empty<ExternalLoginRecord>(),
            JsonSerializer.Deserialize<RetiredUserSnapshot>(entity.RetiredUserSnapshot) ??
            new RetiredUserSnapshot(false, null),
            Enum.Parse<MergeState>(entity.State),
            entity.CreatedAt, entity.PurgeAfter, entity.PurgedAt);
    }
}
