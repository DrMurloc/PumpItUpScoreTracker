using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Infrastructure.Entities;

namespace ScoreTracker.Translations.Infrastructure;

internal sealed class EFTranslationBatchRepository : ITranslationBatchRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFTranslationBatchRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Record(TranslationBatchInfo batch, int itemCount, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationBatchEntity>().AddAsync(new TranslationBatchEntity
        {
            Id = batch.Id,
            ProviderBatchId = batch.ProviderBatchId,
            Stage = batch.Stage.ToString(),
            ItemCount = itemCount,
            SubmittedAt = batch.SubmittedAt
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TranslationBatchInfo>> Open(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return (await database.Set<TranslationBatchEntity>().AsNoTracking()
                .Where(b => b.CompletedAt == null)
                .OrderBy(b => b.SubmittedAt)
                .ToArrayAsync(cancellationToken))
            .Select(b => new TranslationBatchInfo(b.Id, b.ProviderBatchId,
                Enum.Parse<TranslationState>(b.Stage), b.SubmittedAt))
            .ToArray();
    }

    public async Task Complete(Guid id, LanguageModelUsage totalUsage, decimal costUsd, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<TranslationBatchEntity>()
            .Where(b => b.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(b => b.CompletedAt, now)
                .SetProperty(b => b.InputTokens, (long)totalUsage.InputTokens)
                .SetProperty(b => b.OutputTokens, (long)totalUsage.OutputTokens)
                .SetProperty(b => b.CacheCreationInputTokens, (long)totalUsage.CacheCreationInputTokens)
                .SetProperty(b => b.CacheReadInputTokens, (long)totalUsage.CacheReadInputTokens)
                .SetProperty(b => b.CostUsd, costUsd), cancellationToken);
    }

    public async Task<decimal> SpendSince(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationBatchEntity>()
            .Where(b => b.CompletedAt != null && b.CompletedAt >= cutoff)
            .SumAsync(b => b.CostUsd, cancellationToken);
    }

    public async Task<DateTimeOffset?> LastSubmittedAt(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationBatchEntity>()
            .OrderByDescending(b => b.SubmittedAt)
            .Select(b => (DateTimeOffset?)b.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> LastCollectedAt(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<TranslationBatchEntity>()
            .Where(b => b.CompletedAt != null)
            .OrderByDescending(b => b.CompletedAt)
            .Select(b => b.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
