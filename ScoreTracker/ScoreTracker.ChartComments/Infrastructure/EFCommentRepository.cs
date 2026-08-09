using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentRepository : ICommentRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<CommentRow>> GetForChart(Guid chartId, CommentAudience audience,
        Guid viewerId, CommentSort sort, int takeRoots, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        var visible = Visible(database, chartId, audience, viewerId);

        // Roots are paged; replies are not. A root's replies come back whole because a thread
        // truncated halfway is a conversation missing its answer.
        var rootIds = await Order(visible.Where(c => c.ParentCommentId == null), database, sort)
            .Take(takeRoots)
            .Select(c => c.Id)
            .ToArrayAsync(cancellationToken);

        var rows = await visible
            .Where(c => (c.ParentCommentId == null && rootIds.Contains(c.Id))
                        || (c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId.Value)))
            .Select(c => new
            {
                Comment = c,
                Votes = database.Set<CommentVoteEntity>().Count(v => v.CommentId == c.Id),
                ViewerVoted = viewerId != Guid.Empty
                              && database.Set<CommentVoteEntity>()
                                  .Any(v => v.CommentId == c.Id && v.UserId == viewerId)
            })
            .ToArrayAsync(cancellationToken);

        // Ordering is re-applied in memory: the root order came from the paging query above, and
        // replies within a root always read oldest-first regardless of the chosen sort — a
        // conversation is not a leaderboard.
        var rank = rootIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        return rows
            .OrderBy(r => rank[r.Comment.ParentCommentId ?? r.Comment.Id])
            .ThenBy(r => r.Comment.ParentCommentId == null ? 0 : 1)
            .ThenBy(r => r.Comment.CreatedAt)
            .Select(r => new CommentRow(r.Comment.Id, r.Comment.ChartId, r.Comment.UserId,
                r.Comment.ParentCommentId, r.Comment.Text, r.Comment.CreatedAt, r.Comment.EditedAt,
                r.Comment.DeletedAt, r.Comment.DeletedByUserId, r.Votes, r.ViewerVoted))
            .ToArray();
    }

    public async Task<int> CountRoots(Guid chartId, CommentAudience audience, Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await Visible(database, chartId, audience, viewerId)
            .CountAsync(c => c.ParentCommentId == null, cancellationToken);
    }

    public async Task<Comment?> GetById(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        return entity == null ? null : Hydrate(entity);
    }

    public async Task<bool> HasReplies(Guid commentId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        return await database.Set<CommentEntity>()
            .AnyAsync(c => c.ParentCommentId == commentId, cancellationToken);
    }

    public async Task Save(Comment comment, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentEntity>()
            .FirstOrDefaultAsync(c => c.Id == comment.Id, cancellationToken);

        if (entity == null)
        {
            entity = new CommentEntity
            {
                Id = comment.Id,
                ChartId = comment.ChartId,
                Audience = comment.Audience.Kind.ToString(),
                CommunityId = comment.Audience.CommunityId,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt
            };
            await database.Set<CommentEntity>().AddAsync(entity, cancellationToken);
        }

        // Audience, chart and parent are immutable by design and deliberately not re-assigned on
        // update: a thread that could change audience is the one thing the aggregate exists to stop.
        entity.UserId = comment.UserId;
        entity.Text = comment.Text;
        entity.SourceLanguage = comment.SourceLanguage;
        entity.EditedAt = comment.EditedAt;
        entity.DeletedAt = comment.DeletedAt;
        entity.DeletedByUserId = comment.DeletedByUserId;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteRevision(Guid commentId, string replacedText, DateTimeOffset replacedAt,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommentRevisionEntity>().AddAsync(new CommentRevisionEntity
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            Text = replacedText,
            ReplacedAt = replacedAt
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddVote(Guid commentId, Guid userId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Checked rather than caught: the unique index is the guarantee, this is the polite path.
        if (await database.Set<CommentVoteEntity>()
                .AnyAsync(v => v.CommentId == commentId && v.UserId == userId, cancellationToken))
            return;

        await database.Set<CommentVoteEntity>().AddAsync(new CommentVoteEntity
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            UserId = userId,
            CreatedAt = at
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveVote(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommentVoteEntity>()
            .Where(v => v.CommentId == commentId && v.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    ///     ⚠ The audience gate, and the only place it exists. A private audience is narrowed to the
    ///     viewer's own rows here rather than anywhere a caller could forget — a leaked personal
    ///     note is the worst thing this feature can do, and <c>CommentAudienceIsolationTests</c>
    ///     exists to hold this method to it.
    /// </summary>
    private static IQueryable<CommentEntity> Visible(ChartAttemptDbContext database, Guid chartId,
        CommentAudience audience, Guid viewerId)
    {
        var kind = audience.Kind.ToString();
        var query = database.Set<CommentEntity>().AsNoTracking()
            .Where(c => c.ChartId == chartId && c.Audience == kind);

        if (audience.Kind == CommentAudienceKind.Community)
            query = query.Where(c => c.CommunityId == audience.CommunityId);

        if (audience.IsPrivate)
            // Guid.Empty never matches a note: a signed-out reader asking for the private scope
            // gets nothing rather than everybody's.
            query = query.Where(c => c.UserId == viewerId && viewerId != Guid.Empty);

        return query;
    }

    private static IQueryable<CommentEntity> Order(IQueryable<CommentEntity> roots,
        ChartAttemptDbContext database, CommentSort sort)
    {
        return sort == CommentSort.Newest
            ? roots.OrderByDescending(c => c.CreatedAt)
            : roots.OrderByDescending(c => database.Set<CommentVoteEntity>().Count(v => v.CommentId == c.Id))
                .ThenByDescending(c => c.CreatedAt);
    }

    private static Comment Hydrate(CommentEntity entity)
    {
        var audience = entity.Audience switch
        {
            nameof(CommentAudienceKind.Private) => CommentAudience.Private,
            nameof(CommentAudienceKind.Community) => CommentAudience.Community(entity.CommunityId!.Value),
            _ => CommentAudience.Public
        };

        return Comment.FromStorage(new CommentState(entity.Id, entity.ChartId, entity.UserId, audience,
            entity.ParentCommentId, entity.Text, entity.CreatedAt, entity.EditedAt, entity.DeletedAt,
            entity.DeletedByUserId, entity.SourceLanguage));
    }
}
