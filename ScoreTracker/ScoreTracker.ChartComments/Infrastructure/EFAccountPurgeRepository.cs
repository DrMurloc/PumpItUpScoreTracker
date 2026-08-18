using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartComments.Infrastructure;

/// <summary>
///     Erases one account's comments, notes, votes, reports and mutes.
///     <para>
///         <see cref="CommentEntity" /> is deliberately NOT in the <c>UserOwned</c> manifest and
///         sits in <c>AccountPurgeCoverageTests.Exempt</c> with that reason. <c>UserDataPurge</c>
///         issues a blanket <c>DELETE … WHERE UserId</c>, which for this table would take a root
///         out from under its replies — and the coverage test counts rows, so it would pass green
///         while the Comments tab threw. The two-step below is what the exemption is standing in
///         for. The moderation tables have no orphan problem, so they ride the manifest.
///     </para>
/// </summary>
internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical purges by convention. AccountPurgeCoverageTests checks this
    ///     against the assembly, and <see cref="UserDataPurge" /> executes it — one list, so a
    ///     table cannot be declared without also being deleted. A report is the reporter's row
    ///     ([PurgeKey] on ReporterUserId — an open report vanishing with its reporter is
    ///     accepted); a mute is the muted player's row, not the moderator's; an archived comment
    ///     is still its author's words, and words surviving a club's death must not survive the
    ///     author's deletion (blanket delete is safe there — nothing renders archives as threads).
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(CommentArchiveEntity),
        typeof(CommentReportEntity),
        typeof(CommentRestrictionEntity)
    };

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
            // Audience is irrelevant to a tombstone and is not read back out, so the cheapest
            // legal value stands in rather than re-parsing the stored one.
            var comment = Comment.FromStorage(new CommentState(root.Id, root.ChartId, root.UserId,
                CommentAudience.Public, root.ParentCommentId, root.Text, root.CreatedAt, root.EditedAt,
                root.DeletedAt, root.DeletedByUserId, root.SourceLanguage));
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

        // 4. Reports filed against this account's comments go with them — tombstoned roots
        //    included, because a stub with no words left is nothing a moderator can act on. The
        //    reporter loses their report row; the thing reported no longer exists. Reports this
        //    account FILED are the manifest's job below, keyed on ReporterUserId.
        await database.Set<CommentReportEntity>()
            .Where(r => myCommentIds.Contains(r.CommentId) || tombstonedIds.Contains(r.CommentId))
            .ExecuteDeleteAsync(cancellationToken);

        await comments.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        await database.Set<CommentConsentEntity>()
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 5. The convention-resolved tables: reports this account filed, mutes this account
        //    carries. Mutes this account IMPOSED on others stay — the mute belongs to the club,
        //    not the moderator, like DeletedByUserId outliving its account.
        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
