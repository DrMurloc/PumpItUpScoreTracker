using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The reads that answer "how do I compare": the two pickers, per-chart rival scores, the
///     head-to-head, and the feed.
/// </summary>
internal sealed class RivalReadSaga :
    IRequestHandler<SearchRivalCandidatesQuery, IReadOnlyList<RivalCandidateRecord>>,
    IRequestHandler<SearchRivalTagsQuery, IReadOnlyList<string>>,
    IRequestHandler<GetRivalScoresForChartsQuery, RivalChartScores>,
    IRequestHandler<GetRivalHeadToHeadQuery, RivalHeadToHeadRecord?>,
    IRequestHandler<GetMyRivalHighlightsQuery, IEnumerable<PlayerHighlightRecord>>
{
    private readonly RivalAudienceReader _audience;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly RivalSubjectResolver _resolver;
    private readonly IRivalRepository _rivals;
    private readonly RivalScoreReader _rivalScores;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;

    public RivalReadSaga(IRivalRepository rivals, RivalSubjectResolver resolver, RivalScoreReader rivalScores,
        RivalAudienceReader audience, IScoreReader scores, IUserReader users, IMediator mediator,
        ICurrentUserAccessor currentUser)
    {
        _rivals = rivals;
        _resolver = resolver;
        _rivalScores = rivalScores;
        _audience = audience;
        _scores = scores;
        _users = users;
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Public players plus the caller's clubmates. The private-stranger exclusion happens
    ///     HERE rather than in Identity: Identity has no idea what a community is, and teaching it
    ///     would put the membership graph in the wrong vertical.
    /// </summary>
    public async Task<IReadOnlyList<RivalCandidateRecord>> Handle(SearchRivalCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn || string.IsNullOrWhiteSpace(request.Term))
            return Array.Empty<RivalCandidateRecord>();

        var me = _currentUser.User.Id;
        var matches = (await _mediator.Send(
            new ScoreTracker.Identity.Contracts.Queries.SearchForUsersQuery(request.Term, 1, request.Take * 4),
            cancellationToken)).Results;

        var clubmates = await _audience.GetClubmates(cancellationToken);
        var already = (await _rivals.GetRivalsOwnedBy(me, cancellationToken))
            .Where(e => e.TargetUserId != null).Select(e => e.TargetUserId!.Value).ToHashSet();

        return matches
            .Where(u => u.Id != me)
            .Where(u => u.IsPublic || clubmates.Contains(u.Id))
            .Take(request.Take)
            .Select(u => new RivalCandidateRecord(u.Id, u.Name.ToString(), u.ProfileImage, u.IsPublic,
                clubmates.Contains(u.Id), already.Contains(u.Id)))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> Handle(SearchRivalTagsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Term)) return Array.Empty<string>();

        return await _mediator.Send(
            new SearchOfficialBoardTagsQuery(request.Mix, request.Term, request.Take), cancellationToken);
    }

    public async Task<RivalChartScores> Handle(GetRivalScoresForChartsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return RivalChartScores.Empty;
        var rivals = await MyRivals(request.Mix, cancellationToken);
        return await _rivalScores.Read(rivals, request.Mix, request.ChartIds, cancellationToken);
    }

    public async Task<RivalHeadToHeadRecord?> Handle(GetRivalHeadToHeadQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;
        var me = _currentUser.User.Id;

        var edge = await _rivals.GetEdge(request.EdgeId, cancellationToken);
        if (edge == null || edge.OwnerUserId != me) return null;

        var subject = (await _resolver.Resolve(new[] { edge }, request.Mix, cancellationToken))
            .FirstOrDefault();
        if (subject == null) return null;

        // A record with no score at all, or one set on a run that broke, is not a comparable
        // result — the same bar the community folder compare uses. Both halves matter: the
        // ledger holds scoreless break rows, and counting a break as a number would report
        // losses nobody played. An official placement is a completed run by construction, so
        // excluding breaks on OUR side only would hand every ghost comparison a free win.
        var mine = (await _scores.GetBestScores(request.Mix, me, cancellationToken))
            .Where(s => s.Score != null && !s.IsBroken)
            .ToDictionary(s => s.ChartId, s => (int)s.Score!.Value);

        // A site rival compares within a folder; a board-only one compares on the charts we are
        // BOTH on, because the mirror covers a scattering of level 20+ boards rather than a
        // folder. Same table either way — the unit is what differs, and the count says so.
        var chartIds = subject.IsGhost
            ? mine.Keys.ToArray()
            : await FolderChartIds(request, me, mine.Keys, cancellationToken);

        var theirs = await _rivalScores.Read(new[] { subject }, request.Mix, chartIds, cancellationToken);

        var rows = new List<RivalHeadToHeadRow>();
        foreach (var (chartId, scores) in theirs.ByChart)
        {
            var theirScore = scores.FirstOrDefault(s => !s.IsBroken);
            if (theirScore == null) continue;
            rows.Add(new RivalHeadToHeadRow(chartId,
                mine.TryGetValue(chartId, out var myScore) ? myScore : null,
                theirScore.Score, theirScore.Source));
        }

        var shared = rows.Count(r => r.YourScore != null && r.TheirScore != null);
        return new RivalHeadToHeadRecord(subject,
            rows.Count(r => r.YourScore != null && r.TheirScore != null && r.YourScore > r.TheirScore),
            rows.Count(r => r.YourScore != null && r.TheirScore != null && r.TheirScore > r.YourScore),
            shared, theirs.OfficialAsOf,
            rows.OrderByDescending(r => (r.TheirScore ?? 0) - (r.YourScore ?? 0)).ToArray());
    }

    /// <summary>
    ///     Ghosts never appear: wins come from imports, and a board-only player has none. Their
    ///     absence is why the feed's empty state has to name what would fill it (D30).
    /// </summary>
    public async Task<IEnumerable<PlayerHighlightRecord>> Handle(GetMyRivalHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<PlayerHighlightRecord>();

        var userIds = (await _rivals.GetRivalsOwnedBy(_currentUser.User.Id, cancellationToken))
            .Where(e => e.TargetUserId != null)
            .Select(e => e.TargetUserId!.Value)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0) return Array.Empty<PlayerHighlightRecord>();

        return await _mediator.Send(new GetPlayerHighlightsQuery(userIds, request.Mix, request.Take),
            cancellationToken);
    }

    private async Task<IReadOnlyList<RivalSubject>> MyRivals(MixEnum mix, CancellationToken cancellationToken)
    {
        var edges = await _rivals.GetRivalsOwnedBy(_currentUser.User.Id, cancellationToken);
        return await _resolver.Resolve(edges, mix, cancellationToken);
    }

    private async Task<IReadOnlyCollection<Guid>> FolderChartIds(GetRivalHeadToHeadQuery request, Guid me,
        IEnumerable<Guid> myScoredCharts, CancellationToken cancellationToken)
    {
        // No folder named: the universe is every chart you hold a comparable score on, which the
        // caller has already read. Asking the ledger for the same rows a second time also asked a
        // different question — that read kept scoreless breaks, so the two paths disagreed about
        // which charts are in play.
        if (request.ChartType == null || request.Level == null)
            return myScoredCharts.ToArray();

        return (await _scores.GetPlayerScores(request.Mix, new[] { me }, request.ChartType.Value,
                request.Level.Value, cancellationToken))
            .Select(s => s.record.ChartId)
            .ToArray();
    }
}
