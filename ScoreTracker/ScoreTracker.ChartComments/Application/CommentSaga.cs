using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
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
    private const string ToolHostsCacheKey = $"{nameof(CommentSaga)}__TrustedToolHosts";

    // Short on purpose. The allowlist is data — a tool approved this afternoon should be trusted
    // by this evening — but it is read on every comment render, so it cannot be a query each time.
    private static readonly TimeSpan ToolHostsCacheFor = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ICommentConsentRepository _consents;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly ICommentRepository _comments;
    private readonly IUserReader _users;

    public CommentSaga(ICommentRepository comments, ICommentConsentRepository consents,
        ICurrentUserAccessor currentUser, IDateTimeOffsetAccessor clock, IMediator mediator,
        IUserReader users, IMemoryCache cache)
    {
        _comments = comments;
        _consents = consents;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
        _users = users;
        _cache = cache;
    }

    private Guid ViewerId => _currentUser.IsLoggedIn ? _currentUser.User.Id : Guid.Empty;

    // ----- writes ------------------------------------------------------------------------------

    public async Task<Guid> Handle(PostCommentCommand request, CancellationToken cancellationToken)
    {
        var author = RequireSignedIn();
        await EnsureMayPostTo(request.Audience, cancellationToken);

        var comment = Comment.Post(request.ChartId, author, request.Audience, request.Text, _clock.Now);
        await _comments.Save(comment, cancellationToken);

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

        await EnsureMayPostTo(root.Audience, cancellationToken);

        var reply = Comment.Reply(root, author, request.Text, _clock.Now);
        await _comments.Save(reply, cancellationToken);

        return reply.Id;
    }

    public async Task Handle(EditCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);

        var replaced = comment.Edit(actor, request.Text, _clock.Now);

        // The revision goes in first. If the save then fails, the history has a row the comment
        // never had — which is recoverable; the reverse loses what a moderator would need.
        await _comments.WriteRevision(comment.Id, replaced, _clock.Now, cancellationToken);
        await _comments.Save(comment, cancellationToken);
    }

    public async Task Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        var comment = await Load(request.CommentId, cancellationToken);

        comment.DeleteByAuthor(actor, _clock.Now);
        await _comments.Save(comment, cancellationToken);
    }

    public async Task Handle(RemoveCommentCommand request, CancellationToken cancellationToken)
    {
        var actor = RequireSignedIn();
        // Slice 2 moderation is the site admin alone, which needs no permission flag because
        // User.IsAdmin is computed. Community moderators arrive with ModerateComments.
        if (!_currentUser.User.IsAdmin)
            throw new CommentNotAllowedException("You cannot remove that comment.");

        var comment = await Load(request.CommentId, cancellationToken);
        comment.RemoveByModerator(actor, _clock.Now);
        await _comments.Save(comment, cancellationToken);
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

        var trust = new LinkTrust(await TrustedToolHosts(cancellationToken));
        var authors = (await _users.GetUsers(
                rows.Select(r => r.UserId).Where(id => id != Guid.Empty).Distinct(), cancellationToken))
            .ToDictionary(u => u.Id);

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
                    .Select(reply => Project(reply, request.Audience, trust, authors, viewer,
                        Array.Empty<CommentRecord>()))
                    .ToArray();

                return Project(root, request.Audience, trust, authors, viewer, replies);
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

        var scopes = new List<CommentScopeRecord>
        {
            new(CommentAudience.Public, Name.From("Public")),
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
                CommentAudience.Community(community.CommunityId), community.CommunityName)));

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

    private CommentRecord Project(CommentRow row, CommentAudience audience, LinkTrust trust,
        Dictionary<Guid, User> authors, Guid viewer, IReadOnlyList<CommentRecord> replies)
    {
        var deletion = DeletionOf(row);
        var author = row.UserId != Guid.Empty && authors.TryGetValue(row.UserId, out var found) ? found : null;

        return new CommentRecord(
            row.Id,
            row.ChartId,
            deletion == null ? row.UserId : null,
            deletion == null ? author?.Name : null,
            deletion == null ? author?.Country : null,
            deletion == null ? author?.ProfileImage : null,
            deletion == null ? CommentText.Parse(row.Text, trust) : Array.Empty<CommentSpan>(),
            deletion == null ? row.Votes : 0,
            deletion == null && row.ViewerVoted,
            deletion == null && viewer != Guid.Empty && viewer == row.UserId,
            // The shield is the permission: nobody else renders it, and a personal note never
            // carries one for anybody.
            deletion == null && !audience.IsPrivate && _currentUser.IsLoggedIn && _currentUser.User.IsAdmin,
            row.CreatedAt,
            deletion == null ? row.EditedAt : null,
            deletion,
            replies);
    }

    private static CommentDeletion? DeletionOf(CommentRow row)
    {
        if (row.DeletedAt == null) return null;
        if (row.UserId == Guid.Empty) return CommentDeletion.ByDeletedAccount;

        return row.DeletedByUserId == row.UserId ? CommentDeletion.ByAuthor : CommentDeletion.ByModerator;
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

    private async Task<IReadOnlyList<string>> TrustedToolHosts(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ToolHostsCacheKey, out IReadOnlyList<string>? cached) && cached != null)
            return cached;

        var hosts = (await _mediator.Send(new GetPublicToolsQuery(), cancellationToken))
            .Select(tool => tool.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => LinkTrust.TryParse(url!)?.Host)
            .Where(host => host != null)
            .Select(host => host!)
            .Distinct()
            .ToArray();

        _cache.Set(ToolHostsCacheKey, (IReadOnlyList<string>)hosts, ToolHostsCacheFor);

        return hosts;
    }
}
