using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Reporting, dismissal, mutes, and the two queues. The hierarchy is
///     <see cref="CommentModerationAuthority" />'s to answer; this saga only gathers the roles it
///     asks about — through Communities' published contracts, never its tables.
/// </summary>
internal sealed class CommentModerationSaga :
    IRequestHandler<ReportCommentCommand>,
    IRequestHandler<DismissCommentReportCommand>,
    IRequestHandler<RestrictCommentingCommand>,
    IRequestHandler<LiftCommentRestrictionCommand>,
    IRequestHandler<GetOpenCommentReportsQuery, IReadOnlyList<ReportedCommentRecord>>,
    IRequestHandler<GetSiteReportedCommentsQuery, IReadOnlyList<SiteReportedCommentRecord>>,
    IRequestHandler<GetCommunityCommentRestrictionsQuery, IReadOnlyList<CommentRestrictionRecord>>
{
    private readonly IMemoryCache _cache;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ICommentRepository _comments;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly ICommentReportRepository _reports;
    private readonly ICommentRestrictionRepository _restrictions;
    private readonly IUserReader _users;

    public CommentModerationSaga(ICommentRepository comments, ICommentReportRepository reports,
        ICommentRestrictionRepository restrictions, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor clock, IMediator mediator, IUserReader users, IMemoryCache cache)
    {
        _comments = comments;
        _reports = reports;
        _restrictions = restrictions;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
        _users = users;
        _cache = cache;
    }

    // ----- reporting ---------------------------------------------------------------------------

    public async Task Handle(ReportCommentCommand request, CancellationToken cancellationToken)
    {
        var reporter = RequireSignedIn();
        var comment = await _comments.GetById(request.CommentId, cancellationToken)
                      ?? throw new CommentNotAllowedException("That comment is no longer there.");

        // Defence in depth, like RemoveByModerator's own guard: nobody can see a note but its
        // author, and its author cannot report themselves either way.
        if (comment.Audience.IsPrivate)
            throw new CommentNotAllowedException("Personal notes are not reported.");
        if (comment.IsDeleted)
            throw new CommentNotAllowedException("That comment is no longer there.");
        if (comment.UserId == reporter)
            throw new CommentNotAllowedException("You cannot report your own comment.");

        // One open report per reporter per comment — reporting again while yours is open
        // changes nothing, and says so by succeeding.
        if (await _reports.HasOpenFrom(comment.Id, reporter, cancellationToken)) return;

        // RenderingLocale stays null until the translation pipeline exists: today everyone reads
        // the original, and null is exactly how that is recorded.
        var report = CommentReport.File(comment.Id, reporter, request.Reason, null, _clock.Now);
        await _reports.Save(report, cancellationToken);
    }

    public async Task Handle(DismissCommentReportCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var report = await _reports.GetById(request.ReportId, cancellationToken)
                     ?? throw new CommentNotAllowedException("That report is no longer there.");

        if (request.Queue == CommentReportQueue.Site)
        {
            if (!_currentUser.User.IsAdmin)
                throw new CommentNotAllowedException("That report is not on your desk.");
            report.ResolveForSite(actor, _clock.Now);
        }
        else
        {
            // A site-only report was never on any community's desk, so there is nothing there
            // to dismiss — refusing here keeps the community slot from being stamped by a queue
            // the report never entered.
            if (CommentReportRouting.IsSiteOnly(report.Reason))
                throw new CommentNotAllowedException("That report is not on your desk.");

            var comment = await _comments.GetById(report.CommentId, cancellationToken)
                          ?? throw new CommentNotAllowedException("That comment is no longer there.");
            // Dismissal takes the same standing as removal: you can only close what you could
            // have acted on. The site admin's desk is the Site queue — being the site admin
            // grants nothing here.
            await EnsureMayModerateInCommunity(comment, cancellationToken);
            report.ResolveForCommunity(actor, _clock.Now);
        }

        await _reports.Save(report, cancellationToken);
    }

    // ----- mutes -------------------------------------------------------------------------------

    public async Task Handle(RestrictCommentingCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        await EnsureMayMute(request.CommunityId, request.UserId, cancellationToken);

        // Already muted is not an error — two moderators racing land on one mute.
        if (await _restrictions.GetActive(request.UserId, request.CommunityId, cancellationToken) != null)
            return;

        var mute = CommentRestriction.Impose(request.UserId, request.CommunityId, actor, request.Reason,
            _clock.Now);
        await _restrictions.Save(mute, cancellationToken);
    }

    public async Task Handle(LiftCommentRestrictionCommand request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await EnsureMayMute(request.CommunityId, request.UserId, cancellationToken);

        var active = await _restrictions.GetActive(request.UserId, request.CommunityId, cancellationToken);
        if (active == null) return;

        active.Lift(_clock.Now);
        await _restrictions.Save(active, cancellationToken);
    }

    // ----- queues ------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ReportedCommentRecord>> Handle(GetOpenCommentReportsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<ReportedCommentRecord>();

        var moderated = (await _mediator.Send(new GetMyCommunityRolesQuery(), cancellationToken))
            .Where(role => role.Role == CommunityRole.Creator ||
                           (role.Role == CommunityRole.Admin &&
                            role.Permissions.HasFlag(CommunityPermission.ModerateComments)))
            .Where(role => request.CommunityId == null || role.CommunityId == request.CommunityId)
            .ToDictionary(role => role.CommunityId);
        if (moderated.Count == 0) return Array.Empty<ReportedCommentRecord>();

        var rows = await _reports.GetOpenForCommunities(moderated.Keys.ToArray(), cancellationToken);

        // The hierarchy filter: an admin never sees a report they could not act on, so a report
        // against a fellow admin's comment waits for the creator rather than dangling in a panel
        // with no buttons.
        var kept = new List<ReportQueueRow>();
        foreach (var byCommunity in rows.GroupBy(row => row.CommunityId!.Value))
        {
            var mine = moderated[byCommunity.Key];
            var roles = await CommunityStanding.MemberRoles(_mediator, byCommunity.Key, cancellationToken);
            kept.AddRange(byCommunity.Where(row => CommentModerationAuthority.MayRemove(false,
                mine.Role, mine.Permissions, roles.RoleOf(row.AuthorUserId))));
        }

        var names = await NamesFor(kept.Select(row => row.CommunityId!.Value), cancellationToken);
        var users = await UsersFor(kept, cancellationToken);

        return kept.Select(row => new ReportedCommentRecord(row.ReportId, row.CommentId, row.ChartId,
                row.CommunityId!.Value, names.TryGetValue(row.CommunityId!.Value, out var name) ? name : (Name?)null,
                row.AuthorUserId, users.TryGetValue(row.AuthorUserId, out var author) ? author : (Name?)null,
                users.TryGetValue(row.ReporterUserId, out var reporter) ? reporter : (Name?)null,
                row.Reason, row.ReportedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<SiteReportedCommentRecord>> Handle(GetSiteReportedCommentsQuery request,
        CancellationToken cancellationToken)
    {
        RequireSignedIn();
        if (!_currentUser.User.IsAdmin)
            throw new CommentNotAllowedException("That queue is not yours to read.");

        var rows = await _reports.GetOpenForSite(cancellationToken);
        var trust = new LinkTrust(await ToolHostAllowlist.Get(_cache, _mediator, cancellationToken));
        var names = await NamesFor(rows.Where(row => row.CommunityId != null)
            .Select(row => row.CommunityId!.Value), cancellationToken);
        var users = await UsersFor(rows, cancellationToken);

        return rows.Select(row => new SiteReportedCommentRecord(row.ReportId, row.CommentId, row.ChartId,
                row.CommunityId,
                row.CommunityId != null && names.TryGetValue(row.CommunityId.Value, out var name)
                    ? name
                    : (Name?)null,
                row.AuthorUserId, users.TryGetValue(row.AuthorUserId, out var author) ? author : (Name?)null,
                users.TryGetValue(row.ReporterUserId, out var reporter) ? reporter : (Name?)null,
                row.Reason, row.ReportedAt,
                // The read grant: the reported words, parsed like any other body — spans, never a
                // string, so this page renders through the same components as the tab.
                CommentText.Parse(row.CommentText, trust)))
            .ToArray();
    }

    public async Task<IReadOnlyList<CommentRestrictionRecord>> Handle(
        GetCommunityCommentRestrictionsQuery request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        var (role, permissions) = await CommunityStanding.Mine(_mediator, request.CommunityId,
            cancellationToken);
        var moderates = role == CommunityRole.Creator ||
                        (role == CommunityRole.Admin &&
                         permissions.HasFlag(CommunityPermission.ModerateComments));
        if (!moderates)
            throw new CommentNotAllowedException("That list is not yours to read.");

        var mutes = await _restrictions.GetActiveForCommunity(request.CommunityId, cancellationToken);
        var users = (await _users.GetUsers(
                mutes.SelectMany(m => new[] { m.UserId, m.RestrictedByUserId }).Distinct(),
                cancellationToken))
            .ToDictionary(u => u.Id, u => u.Name);

        return mutes.Select(mute => new CommentRestrictionRecord(mute.UserId,
                users.TryGetValue(mute.UserId, out var target) ? target : (Name?)null,
                mute.RestrictedByUserId,
                users.TryGetValue(mute.RestrictedByUserId, out var moderator) ? moderator : (Name?)null,
                mute.Reason, mute.CreatedAt))
            .ToArray();
    }

    // ----- helpers -----------------------------------------------------------------------------

    private Guid RequireSignedIn()
    {
        if (!_currentUser.IsLoggedIn) throw new CommentNotAllowedException("Sign in first.");

        return _currentUser.User.Id;
    }

    /// <summary>
    ///     Removal-equivalent standing over one community comment, for actors who are not the site
    ///     admin — the community queue's authorization.
    /// </summary>
    private async Task EnsureMayModerateInCommunity(Comment comment, CancellationToken cancellationToken)
    {
        if (comment.Audience.Kind != CommentAudienceKind.Community || comment.Audience.CommunityId == null)
            throw new CommentNotAllowedException("That report is not on your desk.");

        var communityId = comment.Audience.CommunityId.Value;
        var (role, permissions) = await CommunityStanding.Mine(_mediator, communityId, cancellationToken);
        var roles = await CommunityStanding.MemberRoles(_mediator, communityId, cancellationToken);

        if (!CommentModerationAuthority.MayRemove(false, role, permissions, roles.RoleOf(comment.UserId)))
            throw new CommentNotAllowedException("That report is not on your desk.");
    }

    private async Task EnsureMayMute(Guid communityId, Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var (role, permissions) = await CommunityStanding.Mine(_mediator, communityId, cancellationToken);
        var roles = await CommunityStanding.MemberRoles(_mediator, communityId, cancellationToken);

        if (!CommentModerationAuthority.MayMute(role, permissions, roles.RoleOf(targetUserId)))
            throw new CommentNotAllowedException("You cannot mute that member.");
    }

    private async Task<IReadOnlyDictionary<Guid, Name>> NamesFor(IEnumerable<Guid> communityIds,
        CancellationToken cancellationToken)
    {
        var distinct = communityIds.Distinct().ToArray();

        return distinct.Length == 0
            ? new Dictionary<Guid, Name>()
            : await _mediator.Send(new GetCommunityNamesQuery(distinct), cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, Name>> UsersFor(IEnumerable<ReportQueueRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(row => new[] { row.AuthorUserId, row.ReporterUserId })
            .Where(id => id != Guid.Empty)
            .Distinct();

        return (await _users.GetUsers(ids, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
    }
}
