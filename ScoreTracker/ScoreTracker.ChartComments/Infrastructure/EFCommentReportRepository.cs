using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentReportRepository : ICommentReportRepository
{
    /// <summary>
    ///     The escalating reasons as stored strings, derived from the routing policy once rather
    ///     than restated as literals a rename would silently strand.
    /// </summary>
    private static readonly string[] EscalatingReasons = Enum.GetValues<CommentReportReason>()
        .Where(CommentReportRouting.EscalatesToSite)
        .Select(r => r.ToString())
        .ToArray();

    /// <summary>The site-admin-only reasons as stored strings — never a community desk's business.</summary>
    private static readonly string[] SiteOnlyReasons = Enum.GetValues<CommentReportReason>()
        .Where(CommentReportRouting.IsSiteOnly)
        .Select(r => r.ToString())
        .ToArray();

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentReportRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(CommentReport report, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentReportEntity>()
            .FirstOrDefaultAsync(r => r.Id == report.Id, cancellationToken);

        if (entity == null)
        {
            entity = new CommentReportEntity
            {
                Id = report.Id,
                CommentId = report.CommentId,
                ReporterUserId = report.ReporterUserId,
                Reason = report.Reason.ToString(),
                RenderingLocale = report.RenderingLocale,
                CreatedAt = report.CreatedAt
            };
            await database.Set<CommentReportEntity>().AddAsync(entity, cancellationToken);
        }

        entity.CommunityResolvedAt = report.CommunityResolvedAt;
        entity.CommunityResolvedByUserId = report.CommunityResolvedByUserId;
        entity.SiteResolvedAt = report.SiteResolvedAt;
        entity.SiteResolvedByUserId = report.SiteResolvedByUserId;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommentReport?> GetById(Guid reportId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentReportEntity>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        return entity == null ? null : Hydrate(entity);
    }

    public async Task<bool> HasOpenFrom(Guid commentId, Guid reporterUserId,
        CancellationToken cancellationToken = default)
    {
        // Openness is ROUTING-AWARE here, unlike GetOpenForComment's either-slot read (which only
        // feeds removal, where over-matching is harmless). Most reports can only ever reach one
        // desk — a public report has no community desk, a non-escalating community report never
        // reaches the site's — so counting an unreachable slot as "still open" would leave the
        // reporter permanently unable to re-report after a dismissal, with the retry swallowed
        // behind a success toast.
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var publicKind = nameof(CommentAudienceKind.Public);
        var communityKind = nameof(CommentAudienceKind.Community);

        return await (
                from report in database.Set<CommentReportEntity>()
                join comment in database.Set<CommentEntity>() on report.CommentId equals comment.Id
                where report.CommentId == commentId && report.ReporterUserId == reporterUserId &&
                      ((comment.Audience == publicKind && report.SiteResolvedAt == null) ||
                       (comment.Audience == communityKind &&
                        (SiteOnlyReasons.Contains(report.Reason)
                            ? report.SiteResolvedAt == null
                            : report.CommunityResolvedAt == null ||
                              (EscalatingReasons.Contains(report.Reason) && report.SiteResolvedAt == null))))
                select report.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommentReport>> GetOpenForComment(Guid commentId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<CommentReportEntity>()
            .Where(r => r.CommentId == commentId &&
                        (r.CommunityResolvedAt == null || r.SiteResolvedAt == null))
            .ToArrayAsync(cancellationToken);

        return entities.Select(Hydrate).ToArray();
    }

    // Both queue reads join reports to the LIVING comment they name — a deleted comment leaves
    // nothing to act on, so its reports simply stop appearing. A personal note can never appear:
    // a note was never reportable, and the audience predicates here are Public/Community only.
    // The joins are written out twice because the filters must sit on the raw join — EF cannot
    // translate a predicate through a constructor projection.

    public async Task<IReadOnlyList<ReportQueueRow>> GetOpenForCommunities(
        IReadOnlyCollection<Guid> communityIds, CancellationToken cancellationToken = default)
    {
        if (communityIds.Count == 0) return Array.Empty<ReportQueueRow>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await (
                from report in database.Set<CommentReportEntity>().AsNoTracking()
                join comment in database.Set<CommentEntity>().AsNoTracking()
                    on report.CommentId equals comment.Id
                where comment.DeletedAt == null &&
                      report.CommunityResolvedAt == null &&
                      !SiteOnlyReasons.Contains(report.Reason) &&
                      comment.CommunityId != null &&
                      communityIds.Contains(comment.CommunityId.Value)
                orderby report.CreatedAt
                select new { report, comment })
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => Hydrate(x.report, x.comment)).ToArray();
    }

    public async Task<IReadOnlyList<ReportQueueRow>> GetOpenForSite(
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var publicKind = nameof(CommentAudienceKind.Public);
        var communityKind = nameof(CommentAudienceKind.Community);

        var rows = await (
                from report in database.Set<CommentReportEntity>().AsNoTracking()
                join comment in database.Set<CommentEntity>().AsNoTracking()
                    on report.CommentId equals comment.Id
                where comment.DeletedAt == null &&
                      report.SiteResolvedAt == null &&
                      (comment.Audience == publicKind ||
                       (comment.Audience == communityKind &&
                        (EscalatingReasons.Contains(report.Reason) ||
                         SiteOnlyReasons.Contains(report.Reason))))
                orderby report.CreatedAt
                select new { report, comment })
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => Hydrate(x.report, x.comment)).ToArray();
    }

    private static ReportQueueRow Hydrate(CommentReportEntity report, CommentEntity comment)
    {
        return new ReportQueueRow(report.Id, comment.Id, comment.ChartId, comment.UserId,
            comment.CommunityId, comment.Text, report.ReporterUserId,
            ParseReason(report.Reason),
            report.CreatedAt, report.RenderingLocale);
    }

    private static CommentReport Hydrate(CommentReportEntity entity)
    {
        return CommentReport.FromStorage(new CommentReportState(entity.Id, entity.CommentId,
            entity.ReporterUserId, ParseReason(entity.Reason),
            entity.RenderingLocale, entity.CreatedAt, entity.CommunityResolvedAt,
            entity.CommunityResolvedByUserId, entity.SiteResolvedAt, entity.SiteResolvedByUserId));
    }

    /// <summary>
    ///     IsDefined as well as TryParse: TryParse accepts a bare integer ("7" parses into an
    ///     undefined value), which would then render through a switch's default arm as the last
    ///     reason. Unknown strings and undefined numbers both degrade to OffTopic, matching how
    ///     the queue SQL treats them (non-escalating).
    /// </summary>
    private static CommentReportReason ParseReason(string stored)
    {
        return Enum.TryParse<CommentReportReason>(stored, out var reason) && Enum.IsDefined(reason)
            ? reason
            : CommentReportReason.OffTopic;
    }
}
