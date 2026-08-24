using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentRenderingRepository : ICommentRenderingRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentRenderingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task StoreTranslation(Guid commentId, string sourceLanguage,
        IReadOnlyDictionary<string, string> renderings, string translatedBy, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // One transaction: the delete runs immediately rather than riding SaveChanges, and a
        // comment left with half its old renderings and half its new is worse than either.
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Set<CommentRenderingEntity>()
            .Where(r => r.CommentId == commentId)
            .ExecuteDeleteAsync(cancellationToken);

        database.Set<CommentRenderingEntity>().AddRange(renderings.Select(pair =>
            new CommentRenderingEntity
            {
                Id = Guid.NewGuid(),
                CommentId = commentId,
                Locale = pair.Key,
                Text = pair.Value.Length > 2000 ? pair.Value[..2000] : pair.Value,
                TranslatedBy = translatedBy,
                CreatedAt = now
            }));
        await database.SaveChangesAsync(cancellationToken);

        await database.Set<CommentEntity>()
            .Where(c => c.Id == commentId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.SourceLanguage, sourceLanguage),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentRenderingRow>> GetFor(IReadOnlyList<Guid> commentIds,
        CancellationToken cancellationToken = default)
    {
        if (commentIds.Count == 0) return Array.Empty<CommentRenderingRow>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return (await database.Set<CommentRenderingEntity>().AsNoTracking()
                .Where(r => commentIds.Contains(r.CommentId))
                .ToArrayAsync(cancellationToken))
            .Select(r => new CommentRenderingRow(r.CommentId, r.Locale, r.Text, r.TranslatedBy))
            .ToArray();
    }

    public async Task<bool> AnyFor(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<CommentRenderingEntity>()
            .AnyAsync(r => r.CommentId == commentId, cancellationToken);
    }

    public async Task DeleteFor(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommentRenderingEntity>()
            .Where(r => r.CommentId == commentId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
