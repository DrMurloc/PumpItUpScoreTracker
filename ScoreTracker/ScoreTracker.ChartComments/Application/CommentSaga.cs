using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Translations.Contracts.Messages;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Every chart-comment use case, grouped because they share the same six dependencies and the
///     same three questions: who is asking, which audience are they standing in, and are they
///     allowed to.
/// </summary>
internal sealed class CommentSaga :
    IRequestHandler<PostCommentCommand, Guid>,
    IRequestHandler<ReplyToCommentCommand, Guid>,
    IRequestHandler<EditCommentCommand>,
    IRequestHandler<DeleteCommentCommand>,
    IRequestHandler<RemoveCommentCommand>,
    IRequestHandler<VoteOnCommentCommand>,
    IRequestHandler<AcceptCommentTermsCommand>,
    IRequestHandler<GetChartCommentsQuery, CommentPageRecord>,
    IRequestHandler<GetMyCommentScopesQuery, IReadOnlyList<CommentScopeRecord>>,
    IRequestHandler<GetCommentConsentQuery, CommentConsentRecord>,
    IRequestHandler<GetMyCommentTextQuery, string?>
{
    /// <summary>
    ///     Bumping this re-prompts everyone who agreed to an older wording, which is the whole
    ///     reason the agreement is a versioned row rather than a boolean.
    /// </summary>
    internal const int TermsVersion = 1;

    private const string WorldCommunityName = "World";

    private readonly IBus _bus;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ICommentConsentRepository _consents;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly ICommentRepository _comments;
    private readonly ICommentReportRepository _reports;
    private readonly ICommentRenderingRepository _renderings;
    private readonly ICommentRestrictionRepository _restrictions;
    private readonly ILanguageModelBatchClient _translationClient;
    private readonly IUserReader _users;

    public CommentSaga(ICommentRepository comments, ICommentConsentRepository consents,
        ICommentReportRepository reports, ICommentRestrictionRepository restrictions,
        ICommentRenderingRepository renderings, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor clock, IMediator mediator, IUserReader users, IMemoryCache cache,
        IBus bus, ILanguageModelBatchClient translationClient)
    {
        _translationClient = translationClient;
        _comments = comments;
        _consents = consents;
        _reports = reports;
        _restrictions = restrictions;
        _renderings = renderings;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
        _users = users;
        _cache = cache;
        _bus = bus;
    }

    private Guid ViewerId => _currentUser.IsLoggedIn ? _currentUser.User.Id : Guid.Empty;

    // ----- writes ------------------------------------------------------------------------------

    public async Task<Guid> Handle(PostCommentCommand request, CancellationToken cancellationToken)
    {
        var author = RequireSignedIn();
        await EnsureMayWriteTo(request.Audience, author, cancellationToken);
        await EnsureMayPostTo(request.Audience, cancellationToken);

        var comment = Comment.Post(request.ChartId, author, request.Audience, request.Text, _clock.Now);
        if (!comment.Audience.IsPrivate) comment.StampTranslationQueued(_clock.Now);
        await _comments.Save(comment, cancellationToken);
        await QueueForTranslation(comment, cancellationToken);

        return comment.Id;
    }

    public async Task<Guid> Handle(ReplyToCommentCommand request, CancellationToken cancellationToken)
    {
        var author = RequireSignedIn();
        var parent = await _comments.GetById(request.ParentCommentId, cancellationToken)
                     ?? throw new CommentNotAllowedException("That comment is no longer there.");

        // Replying to a reply targets the root: the aggregate refuses a non-root parent, so the
        // resolution happens here rather than as a rule the UI has to remember.
        var root = parent.IsRoot
            ? parent
            : await _comments.GetById(parent.ParentCommentId!.Value, cancellationToken)
              ?? throw new CommentNotAllowedException("That comment is no longer there.");

        await EnsureMayWriteTo(root.Audience, author, cancellationToken);
        await EnsureMayPostTo(root.Audience, cancellationToken);

        var reply = Comment.Reply(root, author, request.Text, _clock.Now);
        if (!reply.Audience.IsPrivate) reply.StampTranslationQueued(_clock.Now);
        await _comments.Save(reply, cancellationToken);
        await QueueForTranslation(reply, cancellationToken);

        return reply.Id;
    }

    public async Task Handle(EditCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);

        // An edit is a way to keep talking through old comments, so the lock and the mute reach
        // it. Delete deliberately stays ungated below — taking your own words down always works.
        await EnsureMayWriteTo(comment.Audience, actor, cancellationToken);

        // The cooldown reads state as it stood before this edit: renderings mean money was
        // already spent on these words once.
        var hadRenderings = !comment.Audience.IsPrivate
                            && await _renderings.AnyFor(comment.Id, cancellationToken);
        var replaced = comment.Edit(actor, request.Text, _clock.Now);
        var mayQueue = !comment.Audience.IsPrivate && CommentTranslationPolicy.MayQueueAfterEdit(
            hadRenderings, comment.TranslationQueuedAt, _clock.Now);
        if (mayQueue) comment.StampTranslationQueued(_clock.Now);

        // The revision goes in first. If the save then fails, the history has a row the comment
        // never had — which is recoverable; the reverse loses what a moderator would need.
        await _comments.WriteRevision(comment.Id, replaced, _clock.Now, cancellationToken);
        await _comments.Save(comment, cancellationToken);

        if (!comment.Audience.IsPrivate)
        {
            // Old renderings describe words that no longer exist — an edited comment must never
            // show a translation of its previous self, whether or not a new one may queue yet.
            await _renderings.DeleteFor(comment.Id, cancellationToken);
            if (mayQueue) await QueueForTranslation(comment, cancellationToken);
        }
    }

    public async Task Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);

        comment.DeleteByAuthor(actor, _clock.Now);
        await _comments.Save(comment, cancellationToken);
        await DiscardTranslation(comment, cancellationToken);
    }

    public async Task Handle(RemoveCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);
        await EnsureMayModerate(comment, cancellationToken);

        comment.RemoveByModerator(actor, _clock.Now);
        await _comments.Save(comment, cancellationToken);
        await DiscardTranslation(comment, cancellationToken);

        // A removed comment leaves nothing to act on in any queue, so every open report against
        // it resolves — whichever desks were still waiting, stamped by the remover.
        foreach (var report in await _reports.GetOpenForComment(comment.Id, cancellationToken))
        {
            report.ResolveEverywhere(actor, _clock.Now);
            await _reports.Save(report, cancellationToken);
        }
    }

    public async Task Handle(VoteOnCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);
        comment.EnsureCanBeVotedOnBy(actor);

        if (request.Voted) await _comments.AddVote(comment.Id, actor, _clock.Now, cancellationToken);
        else await _comments.RemoveVote(comment.Id, actor, cancellationToken);
    }

    public async Task Handle(AcceptCommentTermsCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        await _consents.Record(actor, TermsVersion, request.ConsentedToPublicIdentity, _clock.Now,
            cancellationToken);
    }

    // ----- reads -------------------------------------------------------------------------------

    public async Task<CommentPageRecord> Handle(GetChartCommentsQuery request,
        CancellationToken cancellationToken)
    {
        var viewer = ViewerId;
        // A signed-out reader asking for the Notes scope gets an empty page, not everybody's notes.
        // The repository would refuse anyway; this is the cheaper answer to the same question.
        if (request.Audience.IsPrivate && viewer == Guid.Empty)
            return new CommentPageRecord(Array.Empty<CommentRecord>(), 0, false);

        var rows = await _comments.GetForChart(request.ChartId, request.Audience, viewer, request.Sort,
            request.TakeRoots, cancellationToken);
        var totalRoots = await _comments.CountRoots(request.ChartId, request.Audience, viewer,
            cancellationToken);

        var trust = new LinkTrust(await ToolHostAllowlist.Get(_cache, _mediator, cancellationToken));
        // The queued badge promises a translation is coming, and with the pipeline parked (no
        // API key) that would be a promise nothing keeps — comments-on, translation-unarmed is a
        // legitimate long-lived state, not a misconfiguration to surface at readers.
        var translationArmed = _translationClient.IsConfigured;
        // Notes are never translated, so their read never touches the renderings table at all.
        var renderings = request.Audience.IsPrivate
            ? new Dictionary<Guid, CommentRenderingRow[]>()
            : (await _renderings.GetFor(rows.Select(r => r.Id).ToArray(), cancellationToken))
            .GroupBy(r => r.CommentId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var authors = (await _users.GetUsers(
                rows.Select(r => r.UserId).Where(id => id != Guid.Empty).Distinct(), cancellationToken))
            .ToDictionary(u => u.Id);
        var moderation = await ModerationContextFor(request.Audience, viewer, cancellationToken);

        var repliesByRoot = rows.Where(r => r.ParentCommentId != null)
            .GroupBy(r => r.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var roots = rows.Where(r => r.ParentCommentId == null)
            .Select(root =>
            {
                // A deleted reply renders nothing at all — it is not holding anything open, so a
                // stub for it is a headstone in the middle of somebody else's conversation.
                var replies = (repliesByRoot.TryGetValue(root.Id, out var found)
                        ? found.Where(reply => reply.DeletedAt == null)
                        : Enumerable.Empty<CommentRow>())
                    .Select(reply => Project(reply, request, trust, authors, viewer,
                        moderation, renderings, translationArmed, Array.Empty<CommentRecord>()))
                    .ToArray();

                return Project(root, request, trust, authors, viewer, moderation, renderings,
                    translationArmed, replies);
            })
            // A deleted root leaves a stub ONLY while something living still hangs off it. Nobody
            // answered, or every answer is gone too, and the whole thread goes with it.
            .Where(record => record.Deletion == null || record.Replies.Count > 0)
            .ToArray();

        return new CommentPageRecord(roots, totalRoots, totalRoots > request.TakeRoots);
    }

    public async Task<IReadOnlyList<CommentScopeRecord>> Handle(GetMyCommentScopesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<CommentScopeRecord>();

        // A muted or locked player keeps every chip — reading is never revoked — and gets a
        // disabled composer where they cannot post. Notes always can: no audience to protect.
        // A BANNED player gets no chip at all, and that needs no code here: GetMyCommunitiesQuery
        // filters banned rows at the source, for every "my communities" surface at once.
        var locked = _currentUser.User.IsContentLocked;
        var mutedIn = (await _restrictions.GetActiveForUser(_currentUser.User.Id, cancellationToken))
            .Select(mute => mute.CommunityId)
            .ToHashSet();

        var scopes = new List<CommentScopeRecord>
        {
            new(CommentAudience.Public, Name.From("Public"), !locked),
            // Second, not last: it is the one chip every signed-in player has, and it must not get
            // pushed off the end of a phone's rail by somebody who joins six communities.
            new(CommentAudience.Private, Name.From("Notes"))
        };

        scopes.AddRange((await _mediator.Send(new GetMyCommunitiesQuery(), cancellationToken))
            // World is a system community carrying IsRegional = 0, so the name check is
            // load-bearing rather than belt-and-braces: without it "your communities" means
            // everybody. Regional boards are ownerless and carry no roles, so a comment posted to
            // one would have no moderator at all.
            .Where(community => !community.IsRegional && community.CommunityName != WorldCommunityName)
            .OrderBy(community => community.CommunityName.ToString())
            .Select(community => new CommentScopeRecord(
                CommentAudience.Community(community.CommunityId), community.CommunityName,
                !locked && !mutedIn.Contains(community.CommunityId))));

        return scopes;
    }

    public async Task<CommentConsentRecord> Handle(GetCommentConsentQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return new CommentConsentRecord(false, false);

        // A personal note is not a promise about how you treat other people, so it asks for
        // nothing. Someone who only ever keeps notes never sees the rules card at all.
        if (request.Audience.IsPrivate) return new CommentConsentRecord(false, false);

        var consent = await _consents.GetFor(_currentUser.User.Id, cancellationToken);
        var needsTerms = consent == null || consent.TermsVersion != TermsVersion;
        var needsIdentity = request.Audience.IsPublic
                            && !_currentUser.User.IsPublic
                            && consent?.ConsentedToPublicIdentityAt == null;

        return new CommentConsentRecord(needsTerms, needsIdentity);
    }

    /// <summary>
    ///     The one place raw comment text leaves the vertical, and only ever your own. Silent null
    ///     rather than a refusal: this answers "can I edit this", and a thrown exception would make
    ///     a missing comment and somebody else's comment look different to a caller that has no
    ///     business telling them apart.
    /// </summary>
    public async Task<string?> Handle(GetMyCommentTextQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;
        var comment = await _comments.GetById(request.CommentId, cancellationToken);

        return comment != null && !comment.IsDeleted && comment.UserId == _currentUser.User.Id
            ? comment.Text
            : null;
    }

    // ----- helpers -----------------------------------------------------------------------------

    private CommentRecord Project(CommentRow row, GetChartCommentsQuery request, LinkTrust trust,
        Dictionary<Guid, User> authors, Guid viewer, ModerationContext moderation,
        Dictionary<Guid, CommentRenderingRow[]> renderings, bool translationArmed,
        IReadOnlyList<CommentRecord> replies)
    {
        var audience = request.Audience;
        var deletion = DeletionOf(row);
        var author = row.UserId != Guid.Empty && authors.TryGetValue(row.UserId, out var found) ? found : null;
        var translation = deletion == null && !audience.IsPrivate
            ? ResolveTranslation(row, trust,
                renderings.TryGetValue(row.Id, out var mine) ? mine : Array.Empty<CommentRenderingRow>(),
                request.ReaderLocale, request.PreferredLocale, translationArmed, out var body)
            : null;

        return new CommentRecord(
            row.Id,
            row.ChartId,
            deletion == null ? row.UserId : null,
            deletion == null ? author?.Name : null,
            deletion == null ? author?.Country : null,
            deletion == null ? author?.ProfileImage : null,
            deletion != null ? Array.Empty<CommentSpan>()
            : translation != null && translation.BodyIsTranslated ? BodyOf(row, translation, renderings, trust)
            : CommentText.Parse(row.Text, trust),
            deletion == null ? row.Votes : 0,
            deletion == null && row.ViewerVoted,
            deletion == null && viewer != Guid.Empty && viewer == row.UserId,
            // The shield is the permission, per comment: the site admin everywhere, the creator
            // over admins and members, an admin with the flag over members — and never on your
            // own row, where Edit and Delete already live. A note never carries one for anybody.
            deletion == null && !audience.IsPrivate && viewer != Guid.Empty && viewer != row.UserId &&
            moderation.MayModerateAuthor(row.UserId),
            row.CreatedAt,
            deletion == null ? row.EditedAt : null,
            deletion,
            replies,
            translation);
    }

    /// <summary>
    ///     The display rule, applied per comment. <paramref name="originalBody" /> carries the
    ///     author's own words only when the resolved body is a rendering — the transient Show
    ///     original flip needs them, and nobody else pays for the second parse.
    /// </summary>
    private static CommentTranslationRecord ResolveTranslation(CommentRow row, LinkTrust trust,
        IReadOnlyList<CommentRenderingRow> mine, string? readerLocale, string? preferredLocale,
        bool translationArmed, out IReadOnlyList<CommentSpan> originalBody)
    {
        var available = mine.Select(rendering => rendering.Locale).OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();
        var resolution = CommentDisplayResolution.Resolve(readerLocale, preferredLocale,
            row.SourceLanguage, available, translationArmed && row.TranslationQueuedAt != null);
        var translated = resolution.RenderingLocale != null;
        originalBody = translated ? CommentText.Parse(row.Text, trust) : Array.Empty<CommentSpan>();

        return new CommentTranslationRecord(row.SourceLanguage, translated, resolution.RenderingLocale,
            originalBody, available, resolution.Pending);
    }

    private static IReadOnlyList<CommentSpan> BodyOf(CommentRow row,
        CommentTranslationRecord translation, Dictionary<Guid, CommentRenderingRow[]> renderings,
        LinkTrust trust)
    {
        var text = renderings[row.Id].First(r => r.Locale == translation.BodyLocale).Text;

        return CommentText.Parse(text, trust);
    }

    /// <summary>
    ///     The role context one page render needs to answer the shield question per comment — two
    ///     contract reads for a community scope, nothing at all for public (where the answer is
    ///     the site admin) or notes (where the answer is nobody).
    /// </summary>
    private async Task<ModerationContext> ModerationContextFor(CommentAudience audience, Guid viewer,
        CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.IsLoggedIn && _currentUser.User.IsAdmin;
        if (viewer == Guid.Empty || audience.IsPrivate)
            return new ModerationContext(false, null, CommunityPermission.None, null);
        if (audience.Kind != CommentAudienceKind.Community || audience.CommunityId == null)
            return new ModerationContext(isAdmin, null, CommunityPermission.None, null);

        var (role, permissions) = await CommunityStanding.Mine(_mediator, audience.CommunityId.Value,
            cancellationToken);
        // Only a viewer with standing needs the member list; everyone else's answer is already no.
        var couldModerate = isAdmin || role == CommunityRole.Creator ||
                            (role == CommunityRole.Admin &&
                             permissions.HasFlag(CommunityPermission.ModerateComments));
        var authorRoles = couldModerate
            ? await CommunityStanding.MemberRoles(_mediator, audience.CommunityId.Value, cancellationToken)
            : null;

        return new ModerationContext(isAdmin, role, permissions, authorRoles);
    }

    private sealed record ModerationContext(
        bool IsSiteAdmin,
        CommunityRole? MyRole,
        CommunityPermission MyPermissions,
        IReadOnlyDictionary<Guid, CommunityRole>? AuthorRoles)
    {
        public bool MayModerateAuthor(Guid authorId)
        {
            // AuthorRoles null with community standing absent means a public scope (or no
            // standing at all) — the authority answers from the site-admin flag alone there.
            return CommentModerationAuthority.MayRemove(IsSiteAdmin, MyRole, MyPermissions,
                AuthorRoles?.RoleOf(authorId));
        }
    }

    /// <summary>
    ///     Removal-equivalent standing over one comment: the site admin everywhere, community
    ///     moderators inside their club and their tier. Public comments belong to the site admin
    ///     alone.
    /// </summary>
    private async Task EnsureMayModerate(Comment comment, CancellationToken cancellationToken)
    {
        if (_currentUser.User.IsAdmin) return;
        if (comment.Audience.Kind != CommentAudienceKind.Community || comment.Audience.CommunityId == null)
            throw new CommentNotAllowedException("You cannot remove that comment.");

        var communityId = comment.Audience.CommunityId.Value;
        var (role, permissions) = await CommunityStanding.Mine(_mediator, communityId, cancellationToken);
        var roles = await CommunityStanding.MemberRoles(_mediator, communityId, cancellationToken);

        if (!CommentModerationAuthority.MayRemove(false, role, permissions, roles.RoleOf(comment.UserId)))
            throw new CommentNotAllowedException("You cannot remove that comment.");
    }

    /// <summary>
    ///     The two write gates, checked before any post, reply or edit. A personal note passes
    ///     both — a note has no audience to protect — and delete is deliberately never gated:
    ///     taking your own words down always works.
    /// </summary>
    private async Task EnsureMayWriteTo(CommentAudience audience, Guid author,
        CancellationToken cancellationToken)
    {
        if (audience.IsPrivate) return;

        // The claim is trustworthy here: SetUserContentLockHandler stamps ClaimsInvalidatedAt on
        // every lock change, and the cookie revalidates against it on the next request.
        if (_currentUser.User.IsContentLocked)
            throw new CommentNotAllowedException("Your account can't post comments right now.");

        if (audience.Kind == CommentAudienceKind.Community && audience.CommunityId != null &&
            await _restrictions.GetActive(author, audience.CommunityId.Value, cancellationToken) != null)
            throw new CommentNotAllowedException("You can't comment in this community right now.");
    }

    private static CommentDeletion? DeletionOf(CommentRow row)
    {
        if (row.DeletedAt == null) return null;
        if (row.UserId == Guid.Empty) return CommentDeletion.ByDeletedAccount;

        return row.DeletedByUserId == row.UserId ? CommentDeletion.ByAuthor : CommentDeletion.ByModerator;
    }

    /// <summary>
    ///     Hands the text to the pipeline with its links already lifted to markers — the model
    ///     never sees a URL. Never for a note: a personal note has an audience of one who already
    ///     reads the language it was written in, a permanent exclusion rather than a deferral.
    /// </summary>
    private async Task QueueForTranslation(Comment comment, CancellationToken cancellationToken)
    {
        if (comment.Audience.IsPrivate) return;

        await _bus.Publish(new QueueTextForTranslationCommand(CommentSourceKeys.For(comment.Id),
            CommentText.ExtractLinks(comment.Text).Text), cancellationToken);
    }

    /// <summary>
    ///     A comment leaving the page takes its translation artifacts with it: stored renderings
    ///     here, the queued text and stored pivot in the pipeline.
    /// </summary>
    private async Task DiscardTranslation(Comment comment, CancellationToken cancellationToken)
    {
        if (comment.Audience.IsPrivate) return;

        await _renderings.DeleteFor(comment.Id, cancellationToken);
        await _bus.Publish(new DiscardTranslationRequestsCommand(
            new[] { CommentSourceKeys.For(comment.Id) }), cancellationToken);
    }

    private Guid RequireSignedIn()
    {
        if (!_currentUser.IsLoggedIn) throw new CommentNotAllowedException("Sign in to comment.");

        return _currentUser.User.Id;
    }

    private async Task<Comment> Load(Guid commentId, CancellationToken cancellationToken)
    {
        return await _comments.GetById(commentId, cancellationToken)
               ?? throw new CommentNotAllowedException("That comment is no longer there.");
    }

    /// <summary>
    ///     Refuses an audience the reader is not standing in. Public and Private need no
    ///     membership; a community does, and a regional one is never offered in the first place.
    /// </summary>
    private async Task EnsureMayPostTo(CommentAudience audience, CancellationToken cancellationToken)
    {
        if (audience.Kind != CommentAudienceKind.Community) return;

        var scopes = await Handle(new GetMyCommentScopesQuery(), cancellationToken);
        if (scopes.Any(scope => scope.Audience == audience)) return;

        throw new CommentNotAllowedException("You are not a member of that community.");
    }

}
