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
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommentReportEntity>()
            .AnyAsync(r => r.CommentId == commentId && r.ReporterUserId == reporterUserId &&
                           (r.CommunityResolvedAt == null || r.SiteResolvedAt == null),
                cancellationToken);
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
                        EscalatingReasons.Contains(report.Reason)))
                orderby report.CreatedAt
                select new { report, comment })
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => Hydrate(x.report, x.comment)).ToArray();
    }

    private static ReportQueueRow Hydrate(CommentReportEntity report, CommentEntity comment)
    {
        return new ReportQueueRow(report.Id, comment.Id, comment.ChartId, comment.UserId,
            comment.CommunityId, comment.Text, report.ReporterUserId,
            Enum.TryParse<CommentReportReason>(report.Reason, out var reason)
                ? reason
                : CommentReportReason.OffTopic,
            report.CreatedAt);
    }

    private static CommentReport Hydrate(CommentReportEntity entity)
    {
        return CommentReport.FromStorage(new CommentReportState(entity.Id, entity.CommentId,
            entity.ReporterUserId,
            Enum.TryParse<CommentReportReason>(entity.Reason, out var reason)
                ? reason
                : CommentReportReason.OffTopic,
            entity.RenderingLocale, entity.CreatedAt, entity.CommunityResolvedAt,
            entity.CommunityResolvedByUserId, entity.SiteResolvedAt, entity.SiteResolvedByUserId));
    }
}
