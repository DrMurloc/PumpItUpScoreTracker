using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartComments.Infrastructure;

/// <summary>
///     Erases one account's comments, notes and votes.
///     <para>
///         There is no <c>UserOwned</c> manifest here on purpose, and
///         <see cref="CommentEntity" /> sits in <c>AccountPurgeCoverageTests.Exempt</c> with that
///         reason. <c>UserDataPurge</c> issues a blanket <c>DELETE … WHERE UserId</c>, which for
///         this table would take a root out from under its replies — and the coverage test counts
///         rows, so it would pass green while the Comments tab threw. The two-step below is what
///         the exemption is standing in for.
///     </para>
/// </summary>
internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IDateTimeOffsetAccessor _clock;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IDateTimeOffsetAccessor clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        // A purge of nobody would tombstone every row already tombstoned. Guarded rather than
        // assumed: this runs from a bus consumer that re-fires daily for a week.
        if (userId == Guid.Empty) return;

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var now = _clock.Now;

        var comments = database.Set<CommentEntity>();
        var mine = comments.Where(c => c.UserId == userId);

        // 1. Roots somebody replied to become anonymous stubs. The thread keeps its shape and the
        //    account keeps nothing — no author, no text, and nothing left to key a row to.
        var heldOpen = await mine
            .Where(c => c.ParentCommentId == null && comments.Any(r => r.ParentCommentId == c.Id))
            .ToArrayAsync(cancellationToken);

        foreach (var root in heldOpen)
        {
            var comment = Comment.FromStorage(root.Id, root.ChartId, root.UserId, ScoreTracker.ChartComments
                    .Contracts.CommentAudience.Public, root.ParentCommentId, root.Text, root.CreatedAt,
                root.EditedAt, root.DeletedAt, root.DeletedByUserId, root.SourceLanguage);
            comment.TombstoneForPurge(now);

            root.UserId = comment.UserId;
            root.Text = comment.Text;
            root.SourceLanguage = null;
            root.DeletedAt = comment.DeletedAt;
            root.DeletedByUserId = comment.DeletedByUserId;
        }

        await database.SaveChangesAsync(cancellationToken);

        // 2. ⚠ Revisions hold the exact text the purge exists to remove and carry no user key of
        //    their own, so nothing keyed on a user would ever reach them. They go by comment id —
        //    for the tombstoned roots as well as for everything about to be deleted outright.
        var myCommentIds = await comments
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .ToArrayAsync(cancellationToken);
        var tombstonedIds = heldOpen.Select(r => r.Id).ToArray();

        await database.Set<CommentRevisionEntity>()
            .Where(r => myCommentIds.Contains(r.CommentId) || tombstonedIds.Contains(r.CommentId))
            .ExecuteDeleteAsync(cancellationToken);

        // 3. Everything else of theirs goes outright: leaf roots, replies, notes. Votes cast on a
        //    comment that is disappearing go with it, and votes this account cast anywhere go too.
        await database.Set<CommentVoteEntity>()
            .Where(v => v.UserId == userId || myCommentIds.Contains(v.CommentId))
            .ExecuteDeleteAsync(cancellationToken);

        await comments.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        await database.Set<CommentConsentEntity>()
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
