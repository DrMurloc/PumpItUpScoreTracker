using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentArchiveRepository : ICommentArchiveRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentArchiveRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Guid>> ArchiveCommunity(Guid communityId, Name communityName, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // One explicit transaction: ExecuteDelete runs immediately rather than riding
        // SaveChanges, and a half-archived club — words copied, live rows still standing, or
        // worse the reverse — is exactly what the bus re-firing cannot repair.
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var communityKind = nameof(CommentAudienceKind.Community);
        var comments = await database.Set<CommentEntity>()
            .Where(c => c.Audience == communityKind && c.CommunityId == communityId)
            .ToArrayAsync(cancellationToken);
        var ids = comments.Select(c => c.Id).ToArray();

        database.Set<CommentArchiveEntity>().AddRange(comments.Select(c => new CommentArchiveEntity
        {
            Id = c.Id,
            ChartId = c.ChartId,
            UserId = c.UserId,
            Audience = c.Audience,
            CommunityId = c.CommunityId,
            CommunityName = communityName.ToString(),
            ParentCommentId = c.ParentCommentId,
            Text = c.Text,
            SourceLanguage = c.SourceLanguage,
            CreatedAt = c.CreatedAt,
            EditedAt = c.EditedAt,
            DeletedAt = c.DeletedAt,
            DeletedByUserId = c.DeletedByUserId,
            ArchivedAt = now
        }));
        await database.SaveChangesAsync(cancellationToken);

        if (ids.Length > 0)
        {
            await database.Set<CommentVoteEntity>()
                .Where(v => ids.Contains(v.CommentId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<CommentRevisionEntity>()
                .Where(r => ids.Contains(r.CommentId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<CommentReportEntity>()
                .Where(r => ids.Contains(r.CommentId))
                .ExecuteDeleteAsync(cancellationToken);
            // Renderings die with the club's comments: the archive keeps the author's words,
            // never a machine's, and nothing renders archived rows anyway.
            await database.Set<CommentRenderingEntity>()
                .Where(r => ids.Contains(r.CommentId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<CommentEntity>()
                .Where(c => ids.Contains(c.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Outside the ids guard: mutes exist without comments, and a mute of a dead club
        // answers no question anybody can still ask.
        await database.Set<CommentRestrictionEntity>()
            .Where(r => r.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ids;
    }
}
