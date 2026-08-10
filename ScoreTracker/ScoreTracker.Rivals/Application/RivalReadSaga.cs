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
        // Read from whichever store this mix records into. A legacy best has a letter and
        // usually no number, so "comparable result" there means a pass — the score is a
        // tiebreak when both sides happen to have typed one.
        var isLegacy = request.Mix.UsesLegacyScoring();
        var mine = isLegacy
            ? (await _scores.GetBestXXAttempts(request.Mix, me, cancellationToken))
            .Where(b => b.BestAttempt is { IsBroken: false })
            .ToDictionary(b => b.Chart.Id, b => (Score: (int?)b.BestAttempt!.Score, Plate: (PhoenixPlate?)null,
                Grade: (XXLetterGrade?)b.BestAttempt.LetterGrade))
            : (await _scores.GetBestScores(request.Mix, me, cancellationToken))
            .Where(s => s.Score != null && !s.IsBroken)
            .ToDictionary(s => s.ChartId, s => (Score: (int?)(int)s.Score!.Value, Plate: s.Plate,
                Grade: (XXLetterGrade?)null));

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
            var hasMine = mine.TryGetValue(chartId, out var my);
            rows.Add(new RivalHeadToHeadRow(chartId, hasMine ? my.Score : null,
                isLegacy && theirScore.Score == 0 ? null : theirScore.Score, theirScore.Source,
                hasMine ? my.Plate : null, false,
                theirScore.Plate, theirScore.IsBroken,
                hasMine ? my.Grade : null, theirScore.LegacyGrade));
        }

        // The score is the comparison, on every mix. Two era scores on the SAME chart are
        // directly comparable — it is only across charts that they mean nothing, and this
        // table never compares across charts. The letter breaks a tie, which on a raw point
        // total is close to a never.
        //
        // A legacy row still counts as comparable when both sides recorded a grade and
        // neither typed a number: only 4.8% of legacy records carry one, so requiring a score
        // would report almost every real rivalry as empty. Those rows tie on 0 and the letter
        // decides them, which is the case the tiebreak actually exists for.
        bool Comparable(RivalHeadToHeadRow r) => isLegacy
            ? r.YourLegacyGrade != null && r.TheirLegacyGrade != null
            : r.YourScore != null && r.TheirScore != null;

        int Margin(RivalHeadToHeadRow r)
        {
            var byScore = (r.YourScore ?? 0).CompareTo(r.TheirScore ?? 0);
            if (!isLegacy || byScore != 0) return byScore;

            return ((int)r.YourLegacyGrade!.Value).CompareTo((int)r.TheirLegacyGrade!.Value);
        }

        var comparable = rows.Where(Comparable).ToArray();
        var shared = comparable.Length;
        return new RivalHeadToHeadRecord(subject,
            comparable.Count(r => Margin(r) > 0),
            comparable.Count(r => Margin(r) < 0),
            shared, theirs.OfficialAsOf,
            // Biggest deficit first — the table's job is to lead with where you are behind.
            // Score decides, grade breaks the tie, matching Margin() exactly: on a legacy mix
            // nearly every row ties at 0 on the number, and without the grade term the order
            // there was arbitrary.
            rows.OrderByDescending(r => (r.TheirScore ?? 0) - (r.YourScore ?? 0))
                .ThenByDescending(r => ((int?)r.TheirLegacyGrade ?? 0) - ((int?)r.YourLegacyGrade ?? 0))
                .ToArray());
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
