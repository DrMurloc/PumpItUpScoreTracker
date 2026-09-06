using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The Planner (docs/design/march-of-murlocs.md §11.5): the Season's future tense. Your record
///     book priced under the board's own frozen configuration, the solver's suggested set inside it,
///     and the same four numbers a played session is described by.
///     <para>
///         Pricing is the board's, never this handler's — a plan that priced charts differently from
///         the board it plans for would be worse than no plan. Only the score being priced changes
///         with the energy: your own best at the top rung, and what the peers say you would score at
///         the other two, through the projector the PUMBILITY page reads.
///     </para>
/// </summary>
internal sealed class MoMPlanHandler : IRequestHandler<BuildMoMPlanQuery, MoMPlanView?>
{
    /// <summary>The peer band PUMBILITY reads at; the Planner asks the same question of the same people.</summary>
    private const double CompetitiveWindow = 1.0;

    private readonly IMoMReadRepository _mom;
    private readonly IChartRepository _charts;
    private readonly IScoreReader _scores;
    private readonly IScoreProjector _projector;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public MoMPlanHandler(IMoMReadRepository mom, IChartRepository charts, IScoreReader scores,
        IScoreProjector projector, ICurrentUserAccessor currentUser, IMediator mediator)
    {
        _mom = mom;
        _charts = charts;
        _scores = scores;
        _projector = projector;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<MoMPlanView?> Handle(BuildMoMPlanQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;

        var board = await _mom.GetBoard(request.BoardId, cancellationToken);
        if (board == null) return null;

        var userId = _currentUser.User.Id;
        var scoring = board.Configuration.Scoring;
        var charts = (await _charts.GetCharts(board.Mix, cancellationToken: cancellationToken))
            .Where(c => c.Type == board.ChartType)
            .ToDictionary(c => c.Id);
        var bests = (await _scores.GetBestScores(board.Mix, userId, cancellationToken))
            .Where(r => r.Score is not null && charts.ContainsKey(r.ChartId))
            .ToDictionary(r => r.ChartId);

        // The two peer rungs come from the projector; the top rung is your own record and asks it
        // nothing. Projecting is the expensive half of this read, so it is skipped where unused.
        var projected = request.Energy == MoMEnergy.TopOfMyGame
            ? new Dictionary<Guid, PhoenixScore>()
            : await Project(board, userId, charts, request.Energy, cancellationToken);

        var priced = new List<MoMPlanChartView>();
        var solvable = new List<MoMPlanChart>();
        var snapshot = scoring.ChartLevelSnapshot;
        foreach (var chart in charts.Values)
        {
            var held = bests.GetValueOrDefault(chart.Id);
            var (score, plate, isProjected) = Priced(chart, held, projected, request.Energy);
            if (score is not { } value) continue;

            var points = (int)scoring.GetScore(chart, value, plate ?? PhoenixPlate.RoughGame, false);
            if (points <= 0) continue;

            var duration = chart.Song.Duration;
            solvable.Add(new MoMPlanChart(chart.Id, (int)chart.Level, duration, points));
            priced.Add(new MoMPlanChartView(chart, value, plate, isProjected, points,
                duration <= TimeSpan.Zero ? 0 : points / duration.TotalSeconds,
                snapshot != null && snapshot.TryGetValue(chart.Id, out var level) ? level : (int)chart.Level + .5,
                false, false, null));
        }

        var anchor = await Anchor(board, userId, priced, cancellationToken);
        var cap = Cap(request.Push, anchor);
        var rest = TimeSpan.FromSeconds(Math.Max(0, request.RestSeconds));
        var plan = MoMPlanner.Solve(solvable, board.Configuration.MaxTime, rest, cap ?? MoMPlanner.NoLevelCap);
        var inSet = plan.Set.ToHashSet();

        var facts = (await _mediator.Send(new GetRestChartFactsQuery(board.Mix,
                priced.Select(p => p.Chart.Id).ToArray()), cancellationToken))
            .ToDictionary(f => f.ChartId);

        var withSet = priced
            .Select(p => p with
            {
                InSet = inSet.Contains(p.Chart.Id),
                IsClosing = p.Chart.Id == plan.ClosingChartId,
                Rest = facts.GetValueOrDefault(p.Chart.Id)
            })
            // The set in the order it would be played, then the rest of the book by rate.
            .OrderByDescending(p => p.InSet)
            .ThenBy(p => p.InSet ? plan.Set.ToList().IndexOf(p.Chart.Id) : int.MaxValue)
            .ThenByDescending(p => p.PointsPerSecond)
            .ToArray();

        var chosen = withSet.Where(p => p.InSet).ToArray();
        var seasons = await _mom.GetSeasons(cancellationToken);
        return new MoMPlanView(
            board.Id,
            seasons.FirstOrDefault(s => s.Id == board.SeasonId)?.Name ?? string.Empty,
            board.Mix,
            board.ChartType,
            board.Configuration.MaxTime,
            rest,
            request.Energy,
            request.Push,
            cap,
            anchor,
            chosen.Sum(p => p.Points),
            chosen.Length,
            chosen.Length == 0 ? 0 : chosen.Average(p => p.BalancedLevel),
            PhoenixScore.From(chosen.Length == 0 ? 0 : (int)Math.Round(chosen.Average(p => (int)p.Score))),
            Downtime(chosen, board.Configuration.MaxTime),
            await Banked(board, userId, cancellationToken),
            withSet);
    }

    /// <summary>
    ///     What a chart is priced at. The top rung is the record you hold and nothing else; the peer
    ///     rungs take the projection where there is one and fall back to your own record, so a chart
    ///     the peers have no opinion on is still planned at what you actually scored.
    /// </summary>
    private static (PhoenixScore? Score, PhoenixPlate? Plate, bool IsProjected) Priced(Chart chart,
        RecordedPhoenixScore? held, IReadOnlyDictionary<Guid, PhoenixScore> projected, MoMEnergy energy)
    {
        if (energy == MoMEnergy.TopOfMyGame)
            return held is { Score: { } best } ? (best, held.Plate, false) : (null, null, false);

        if (projected.TryGetValue(chart.Id, out var estimate)) return (estimate, null, true);
        return held is { Score: { } own } ? (own, held.Plate, false) : (null, null, false);
    }

    private async Task<IReadOnlyDictionary<Guid, PhoenixScore>> Project(MoMBoardInfo board, Guid userId,
        IReadOnlyDictionary<Guid, Chart> charts, MoMEnergy energy, CancellationToken cancellationToken)
    {
        var quantile = energy == MoMEnergy.Great ? PeerEstimator.DefaultQuantile : PeerEstimator.LowerQuartile;
        var projection = await _projector.Project(new ScoreProjectionRequest(board.Mix, board.ChartType, userId,
            charts.Values.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(),
            CompetitiveWindow, charts, Quantiles: new[] { quantile }), cancellationToken);
        return projection.Scores;
    }

    /// <summary>
    ///     The level the push cap hangs off: your last published session's average, or — with no
    ///     session yet — the level you hold most of. Null when neither exists, which uncaps the plan
    ///     rather than capping it at nothing.
    /// </summary>
    private async Task<double?> Anchor(MoMBoardInfo board, Guid userId, IReadOnlyList<MoMPlanChartView> book,
        CancellationToken cancellationToken)
    {
        var boards = await _mom.GetBoards(new[] { board.SeasonId }, cancellationToken);
        var mine = (await _mom.GetPublishedSessions(
                boards.Where(b => b.ChartType == board.ChartType).Select(b => b.Id), cancellationToken))
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.PublishedAt)
            .FirstOrDefault();
        if (mine != null) return mine.AverageDifficulty;

        return book.Count == 0
            ? null
            : book.GroupBy(p => (int)p.Chart.Level).OrderByDescending(g => g.Count()).First().Key + .5;
    }

    /// <summary>
    ///     The level the plan stops at. Steady sits a level below the anchor, Push on it, All out
    ///     nowhere — and an anchorless player is uncapped rather than capped at nothing.
    /// </summary>
    private static int? Cap(MoMPush push, double? anchor)
    {
        if (push == MoMPush.AllOut || anchor is not { } level) return null;

        var floor = (int)Math.Floor(level);
        return push == MoMPush.Push ? floor : floor - 1;
    }

    /// <summary>Your best published session on this board, which is what the conversion is read against.</summary>
    private async Task<int?> Banked(MoMBoardInfo board, Guid userId, CancellationToken cancellationToken)
    {
        var published = await _mom.GetPublishedSessions(new[] { board.Id }, cancellationToken);
        var mine = published.Where(s => s.UserId == userId).ToArray();
        return mine.Length == 0 ? null : mine.Max(s => s.TotalScore);
    }

    private static TimeSpan Downtime(IReadOnlyList<MoMPlanChartView> set, TimeSpan window)
    {
        var song = TimeSpan.FromTicks(set.Sum(p => p.Chart.Song.Duration.Ticks));
        return song >= window ? TimeSpan.Zero : window - song;
    }
}
