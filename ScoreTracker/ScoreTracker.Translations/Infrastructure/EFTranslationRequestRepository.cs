using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Infrastructure.Entities;

namespace ScoreTracker.Translations.Infrastructure;

internal sealed class EFTranslationRequestRepository : ITranslationRequestRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFTranslationRequestRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Upsert(string sourceKey, string text, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<TranslationRequestEntity>()
            .FirstOrDefaultAsync(r => r.SourceKey == sourceKey, cancellationToken);

        if (entity == null)
        {
            entity = new TranslationRequestEntity
            {
                Id = Guid.NewGuid(),
                SourceKey = sourceKey,
                CreatedAt = now
            };
            await database.Set<TranslationRequestEntity>().AddAsync(entity, cancellationToken);
        }

        // A replaced row translates what the text says now — everything stage-specific resets.
        entity.Text = text;
        entity.State = nameof(TranslationState.Pending);
        entity.SourceLanguage = null;
        entity.PivotJson = null;
        entity.BatchId = null;
        entity.FailureReason = null;
        entity.UpdatedAt = now;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task Discard(IReadOnlyList<string> sourceKeys, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationRequestEntity>()
            .Where(r => sourceKeys.Contains(r.SourceKey))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TranslationWork>> NextIn(TranslationState state, int take,
        DateTimeOffset? notSubmittedSince = null, CancellationToken cancellationToken = default)
    {
        var name = state.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var query = database.Set<TranslationRequestEntity>().AsNoTracking()
            .Where(r => r.State == name);
        if (notSubmittedSince is { } cutoff)
            query = query.Where(r => r.LastSubmittedAt == null || r.LastSubmittedAt < cutoff);

        return (await query
                .OrderBy(r => r.CreatedAt)
                .Take(take)
                .ToArrayAsync(cancellationToken))
            .Select(ToWork).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> MarkSubmitted(IReadOnlyList<TranslationWork> works, Guid batchId,
        TranslationState newState, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var name = newState.ToString();
        var marked = new List<Guid>(works.Count);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        foreach (var work in works)
        {
            // Per-row and guarded on UpdatedAt: an edit that upserted between the read and this
            // mark keeps its fresher Pending row untouched — the fifty-row loop is cheap next to
            // displaying renderings of old words against new ones, which is what the blind
            // update allowed.
            var touched = await database.Set<TranslationRequestEntity>()
                .Where(r => r.Id == work.Id && r.UpdatedAt == work.UpdatedAt)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.State, name)
                    .SetProperty(r => r.BatchId, batchId)
                    .SetProperty(r => r.LastSubmittedAt, now)
                    .SetProperty(r => r.UpdatedAt, now), cancellationToken);
            if (touched > 0) marked.Add(work.Id);
        }

        return marked;
    }

    public async Task<IReadOnlyList<TranslationWork>> InBatch(Guid batchId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return (await database.Set<TranslationRequestEntity>().AsNoTracking()
                .Where(r => r.BatchId == batchId)
                .ToArrayAsync(cancellationToken))
            .Select(ToWork).ToArray();
    }

    public async Task CompletePivot(Guid id, string sourceLanguage, string pivotJson, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationRequestEntity>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, nameof(TranslationState.PivotDone))
                .SetProperty(r => r.SourceLanguage, sourceLanguage)
                .SetProperty(r => r.PivotJson, pivotJson)
                .SetProperty(r => r.BatchId, (Guid?)null)
                .SetProperty(r => r.UpdatedAt, now), cancellationToken);
    }

    public async Task CompleteTranslation(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationRequestEntity>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, nameof(TranslationState.Translated))
                .SetProperty(r => r.BatchId, (Guid?)null)
                .SetProperty(r => r.UpdatedAt, now), cancellationToken);
    }

    public async Task Fail(Guid id, string reason, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var stored = reason.Length > 400 ? reason[..400] : reason;
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationRequestEntity>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, nameof(TranslationState.Failed))
                .SetProperty(r => r.FailureReason, stored)
                .SetProperty(r => r.BatchId, (Guid?)null)
                .SetProperty(r => r.UpdatedAt, now), cancellationToken);
    }

    public async Task<int> CountIn(TranslationState state, CancellationToken cancellationToken = default)
    {
        var name = state.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationRequestEntity>().CountAsync(r => r.State == name, cancellationToken);
    }

    public async Task<DateTimeOffset?> OldestPendingCreatedAt(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationRequestEntity>()
            .Where(r => r.State == nameof(TranslationState.Pending))
            .OrderBy(r => r.CreatedAt)
            .Select(r => (DateTimeOffset?)r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TranslationWork>> RecentFailures(int take,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return (await database.Set<TranslationRequestEntity>().AsNoTracking()
                .Where(r => r.State == nameof(TranslationState.Failed))
                .OrderByDescending(r => r.UpdatedAt)
                .Take(take)
                .ToArrayAsync(cancellationToken))
            .Select(ToWork).ToArray();
    }

    public async Task<int> RequeueTranslated(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await Requeue(nameof(TranslationState.Translated), now, cancellationToken);
    }

    public async Task<int> RequeueFailed(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await Requeue(nameof(TranslationState.Failed), now, cancellationToken);
    }

    private async Task<int> Requeue(string fromState, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationRequestEntity>()
            .Where(r => r.State == fromState)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, nameof(TranslationState.Pending))
                .SetProperty(r => r.SourceLanguage, (string?)null)
                .SetProperty(r => r.PivotJson, (string?)null)
                .SetProperty(r => r.BatchId, (Guid?)null)
                .SetProperty(r => r.FailureReason, (string?)null)
                .SetProperty(r => r.UpdatedAt, now), cancellationToken);
    }

    private static TranslationWork ToWork(TranslationRequestEntity entity)
    {
        return new TranslationWork(entity.Id, entity.SourceKey, entity.Text,
            Enum.Parse<TranslationState>(entity.State), entity.SourceLanguage, entity.PivotJson,
            entity.FailureReason, entity.CreatedAt, entity.UpdatedAt, entity.LastSubmittedAt);
    }
}
