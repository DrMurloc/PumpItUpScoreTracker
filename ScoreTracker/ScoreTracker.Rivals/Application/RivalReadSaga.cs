using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

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
    IRequestHandler<GetPlayerHeadToHeadQuery, RivalHeadToHeadRecord?>,
    IRequestHandler<GetMyRivalHighlightsQuery, IEnumerable<PlayerHighlightRecord>>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly RivalSubjectResolver _resolver;
    private readonly IRivalRepository _rivals;
    private readonly RivalScoreReader _rivalScores;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;
    private readonly IPlayerVisibilityReader _visibility;

    public RivalReadSaga(IRivalRepository rivals, RivalSubjectResolver resolver, RivalScoreReader rivalScores,
        IPlayerVisibilityReader visibility, IScoreReader scores, IUserReader users, IChartRepository charts,
        IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _rivals = rivals;
        _resolver = resolver;
        _rivalScores = rivalScores;
        _visibility = visibility;
        _scores = scores;
        _users = users;
        _charts = charts;
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     The site-side picker: everyone the visibility port lets you see — public players plus
    ///     the members of your user-created communities and your rivals — through Identity's
    ///     player search, minus yourself. The one place a private player can be added from is
    ///     therefore the same pool the player page opens for.
    /// </summary>
    public async Task<IReadOnlyList<RivalCandidateRecord>> Handle(SearchRivalCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn || string.IsNullOrWhiteSpace(request.Term))
            return Array.Empty<RivalCandidateRecord>();

        var me = _currentUser.User.Id;
        // One extra row covers the caller matching their own name.
        var hits = await _mediator.Send(
            new ScoreTracker.Identity.Contracts.Queries.SearchPlayersQuery(request.Term, request.Take + 1),
            cancellationToken);

        var already = (await _rivals.GetRivalsOwnedBy(me, cancellationToken))
            .Where(e => e.TargetUserId != null).Select(e => e.TargetUserId!.Value).ToHashSet();

        return hits
            .Where(h => h.UserId != me)
            .Take(request.Take)
            .Select(h => new RivalCandidateRecord(h.UserId, h.Name.ToString(), h.Avatar, h.Visibility.IsPublic,
                h.Visibility.SharedCommunities.Count > 0, already.Contains(h.UserId)))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> Handle(SearchRivalTagsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Term)) return Array.Empty<string>();

        return (await _mediator.Send(
                new SearchOfficialBoardTagsQuery(request.Mix, request.Term, request.Take), cancellationToken))
            .Select(p => p.Username)
            .ToArray();
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

        return subject.UserId is { } them
            ? await SiteHeadToHead(subject.ForHeadToHead(), request.Mix, me, them, request.ChartType, request.Level,
                cancellationToken)
            : await GhostHeadToHead(subject, request.Mix, me, cancellationToken);
    }

    /// <summary>
    ///     The same comparison for anyone the visibility port lets you look at. The gate is the
    ///     port's, not an edge — a rival is one basis among four — and you are not your own opponent.
    /// </summary>
    public async Task<RivalHeadToHeadRecord?> Handle(GetPlayerHeadToHeadQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;
        var me = _currentUser.User.Id;
        if (request.OpponentUserId == me) return null;

        var opponent = await _users.GetUser(request.OpponentUserId, cancellationToken);
        if (opponent == null) return null;
        var visibility = (await _visibility.GetAudience(me, cancellationToken)).Describe(opponent.Id, opponent.IsPublic);
        if (!visibility.CanView) return null;

        var subject = new HeadToHeadSubject(opponent.Id, null, opponent.Name.ToString(), opponent.ProfileImage,
            RivalCapabilities.LiveScores | RivalCapabilities.FolderCompare | RivalCapabilities.Progression);
        return await SiteHeadToHead(subject, request.Mix, me, opponent.Id, request.ChartType, request.Level,
            cancellationToken);
    }

    /// <summary>One side of a comparison: a comparable result a player holds on a chart.</summary>
    private sealed record Side(int? Score, PhoenixPlate? Plate, XXLetterGrade? Grade);

    /// <summary>
    ///     A record with no score at all, or one set on a run that broke, is not a comparable
    ///     result. Both halves matter: the ledger holds scoreless break rows, and counting a break
    ///     as a number would report losses nobody played. Read from whichever store this mix
    ///     records into. A legacy best has a letter and usually no number, so "comparable result"
    ///     there means a pass — the score is a tiebreak when both sides happen to have typed one.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Side>> ComparableBests(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        if (mix.UsesLegacyScoring())
            return (await _scores.GetBestXXAttempts(mix, userId, cancellationToken))
                .Where(b => b.BestAttempt is { IsBroken: false })
                .ToDictionary(b => b.Chart.Id,
                    b => new Side((int?)b.BestAttempt!.Score, null, b.BestAttempt.LetterGrade));

        return (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(s => s.Score != null && !s.IsBroken)
            .ToDictionary(s => s.ChartId, s => new Side((int)s.Score!.Value, s.Plate, null));
    }

    /// <summary>
    ///     Two site players, both sides read from the ledger. The universe is the folder's chart list
    ///     when a folder is named, otherwise every chart either of you has scored — so a chart only
    ///     one of you has played is a row with the other side empty, counted in OnlyYou / OnlyThem
    ///     rather than dropped, while the shared tallies still count only the charts you both hold.
    /// </summary>
    private async Task<RivalHeadToHeadRecord> SiteHeadToHead(HeadToHeadSubject subject, MixEnum mix, Guid me,
        Guid them, ChartType? chartType, DifficultyLevel? level, CancellationToken cancellationToken)
    {
        var mine = await ComparableBests(mix, me, cancellationToken);
        var theirs = await ComparableBests(mix, them, cancellationToken);

        IEnumerable<Guid> universe = chartType != null && level != null
            ? (await _charts.GetCharts(mix, level, chartType, null, cancellationToken)).Select(c => c.Id)
            : mine.Keys.Union(theirs.Keys);

        var rows = new List<RivalHeadToHeadRow>();
        foreach (var chartId in universe.Distinct())
        {
            var hasMine = mine.TryGetValue(chartId, out var my);
            var hasTheirs = theirs.TryGetValue(chartId, out var their);
            if (!hasMine && !hasTheirs) continue;
            rows.Add(new RivalHeadToHeadRow(chartId, hasMine ? my!.Score : null, hasTheirs ? their!.Score : null,
                RivalScoreSource.Site, hasMine ? my!.Plate : null, false, hasTheirs ? their!.Plate : null, false,
                hasMine ? my!.Grade : null, hasTheirs ? their!.Grade : null));
        }

        return Tally(subject, rows, mix.UsesLegacyScoring(), null);
    }

    /// <summary>
    ///     A board-only rival compares on the charts we are BOTH on, because the mirror covers a
    ///     scattering of level 20+ boards rather than a folder. Same table — the unit is what
    ///     differs, and the count says so. No one-sided rows: every chart you have played that is
    ///     not on a board they placed on would be one, which is not information.
    /// </summary>
    private async Task<RivalHeadToHeadRecord> GhostHeadToHead(RivalSubject subject, MixEnum mix, Guid me,
        CancellationToken cancellationToken)
    {
        var isLegacy = mix.UsesLegacyScoring();
        var mine = await ComparableBests(mix, me, cancellationToken);
        var theirs = await _rivalScores.Read(new[] { subject }, mix, mine.Keys.ToArray(), cancellationToken);

        var rows = new List<RivalHeadToHeadRow>();
        foreach (var (chartId, scores) in theirs.ByChart)
        {
            var theirScore = scores.FirstOrDefault(s => !s.IsBroken);
            if (theirScore == null) continue;
            var hasMine = mine.TryGetValue(chartId, out var my);
            rows.Add(new RivalHeadToHeadRow(chartId, hasMine ? my!.Score : null,
                isLegacy && theirScore.Score == 0 ? null : theirScore.Score, theirScore.Source,
                hasMine ? my!.Plate : null, false,
                theirScore.Plate, theirScore.IsBroken,
                hasMine ? my!.Grade : null, theirScore.LegacyGrade));
        }

        return Tally(subject.ForHeadToHead(), rows, isLegacy, theirs.OfficialAsOf);
    }

    /// <summary>
    ///     The score is the comparison, on every mix. Two era scores on the SAME chart are directly
    ///     comparable — it is only across charts that they mean nothing, and this table never
    ///     compares across charts. The letter breaks a tie, which on a raw point total is close to
    ///     a never. A legacy row still counts as comparable when both sides recorded a grade and
    ///     neither typed a number: only 4.8% of legacy records carry one, so requiring a score
    ///     would report almost every real rivalry as empty. Those rows tie on 0 and the letter
    ///     decides them, which is the case the tiebreak actually exists for.
    ///     <para>
    ///         Order: the charts you both hold first, biggest deficit first — the table's job is
    ///         to lead with where you are behind — then the ones only they have, then the ones only
    ///         you have. Score decides, grade breaks the tie, matching Margin() exactly.
    ///     </para>
    /// </summary>
    private static RivalHeadToHeadRecord Tally(HeadToHeadSubject subject, IReadOnlyList<RivalHeadToHeadRow> rows,
        bool isLegacy, DateTimeOffset? officialAsOf)
    {
        bool HasMine(RivalHeadToHeadRow r) => isLegacy ? r.YourLegacyGrade != null : r.YourScore != null;
        bool HasTheirs(RivalHeadToHeadRow r) => isLegacy ? r.TheirLegacyGrade != null : r.TheirScore != null;
        bool Comparable(RivalHeadToHeadRow r) => HasMine(r) && HasTheirs(r);

        int Margin(RivalHeadToHeadRow r)
        {
            var byScore = (r.YourScore ?? 0).CompareTo(r.TheirScore ?? 0);
            if (!isLegacy || byScore != 0) return byScore;

            return ((int)r.YourLegacyGrade!.Value).CompareTo((int)r.TheirLegacyGrade!.Value);
        }

        var comparable = rows.Where(Comparable).ToArray();
        var onlyMine = rows.Where(r => HasMine(r) && !HasTheirs(r)).ToArray();
        var onlyTheirs = rows.Where(r => !HasMine(r) && HasTheirs(r)).ToArray();

        var ordered = comparable
            .OrderByDescending(r => (r.TheirScore ?? 0) - (r.YourScore ?? 0))
            .ThenByDescending(r => ((int?)r.TheirLegacyGrade ?? 0) - ((int?)r.YourLegacyGrade ?? 0))
            .Concat(onlyTheirs.OrderByDescending(r => r.TheirScore ?? 0)
                .ThenByDescending(r => (int?)r.TheirLegacyGrade ?? 0))
            .Concat(onlyMine.OrderByDescending(r => r.YourScore ?? 0)
                .ThenByDescending(r => (int?)r.YourLegacyGrade ?? 0))
            .ToArray();

        return new RivalHeadToHeadRecord(subject,
            comparable.Count(r => Margin(r) > 0),
            comparable.Count(r => Margin(r) < 0),
            comparable.Length, officialAsOf, ordered,
            onlyMine.Length, onlyTheirs.Length);
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

}
