using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

internal sealed class PlayerRatingSaga :
    IRequestHandler<GetTop50ForPlayerQuery, IEnumerable<RecordedPhoenixScore>>,
    IRequestHandler<GetTop50CompetitiveQuery, IEnumerable<RecordedPhoenixScore>>,
    IRequestHandler<RecalculateStatsCommand>,
    IRequestHandler<RecalculatePumbilityCommand>,
    IRequestHandler<PlayerRatingSaga.CaptureSessionStats, PlayerRatingSaga.SessionStatsResult>,
    IConsumer<UserCreatedEvent>
{
    /// <summary>
    ///     The rating step of the session-snapshot pipeline: recalculates stats and
    ///     Pumbility record stats for the batch and hands back what the snapshot event
    ///     needs — the rating milestones minted and the charts that earned the
    ///     CompetitiveImprover flag. Dispatched in-process by the capture orchestrator
    ///     (this saga no longer consumes the raw score event; ordering comes from
    ///     pipeline shape).
    /// </summary>
    /// <summary>
    ///     <paramref name="Changes" /> carries each chart's score on BOTH sides of the batch, not
    ///     just which charts moved: the old scores are what let the old PUMBILITY pool be priced
    ///     and the batch's movement split across the charts that caused it. Nothing else can
    ///     supply them afterwards — by the time the saga runs, the new scores are the record.
    /// </summary>
    public sealed record CaptureSessionStats(
        Guid UserId, MixEnum Mix, IReadOnlyList<Guid> ChangedChartIds, Guid? SessionId,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange>? Changes = null)
        : IRequest<SessionStatsResult>;

    public sealed record SessionStatsResult(
        IReadOnlyList<PlayerMilestoneRecord> Milestones, IReadOnlyList<Guid> ImproverChartIds);

    private sealed record ChartRating(Guid ChartId, ChartType Type, double Rating, PhoenixScore Score, bool IsBroken)
    {
    }

    private sealed record ChartCompetitive(Guid ChartId, ChartType Type, double CompetitiveLevel, PhoenixScore Score)
    {
    }

    private readonly IScoreReader _scores;
    private readonly IPhoenixRecordStatsRepository _recordStats;
    private readonly IChartRepository _charts;
    private readonly IPlayerStatsRepository _stats;
    private readonly IScoreHighlightRepository _highlights;
    private readonly IPlayerMilestoneRepository _milestones;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IBus _bus;
    private readonly IMediator _mediator;
    private readonly IOfficialPlacementReader _officialBoards;
    private readonly ILogger<PlayerRatingSaga> _logger;

    public PlayerRatingSaga(IScoreReader scores, IPhoenixRecordStatsRepository recordStats,
        IChartRepository charts, IPlayerStatsRepository stats, IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones, IDateTimeOffsetAccessor dateTime, IBus bus, IMediator mediator,
        IOfficialPlacementReader officialBoards, ILogger<PlayerRatingSaga> logger)
    {
        _officialBoards = officialBoards;
        _logger = logger;
        _scores = scores;
        _recordStats = recordStats;
        _charts = charts;
        _stats = stats;
        _highlights = highlights;
        _milestones = milestones;
        _dateTime = dateTime;
        _bus = bus;
        _mediator = mediator;
    }

    public async Task<SessionStatsResult> Handle(CaptureSessionStats request,
        CancellationToken cancellationToken)
    {
        var result = await RecalculateCore(new RecalculateStatsCommand(request.UserId, request.Mix,
            request.ChangedChartIds, request.SessionId), request.Changes, cancellationToken);
        await Handle(new RecalculatePumbilityCommand(request.UserId, request.ChangedChartIds.ToArray(),
            request.Mix), cancellationToken);
        return result;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetTop50ForPlayerQuery request,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(request.Mix, false);

        // Phoenix 2 prices plates into the ranking; Phoenix keeps its historical plate-blind
        // ordering byte-identical. Stage breaks never reach here — the filter below drops
        // them, which is why this may not pass IsBroken on the Phoenix branch: the argument
        // is positional, so supplying it would drag the plate in with it.
        double Rank(RecordedPhoenixScore s)
        {
            return request.Mix == MixEnum.Phoenix2
                ? scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value,
                    s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)
                : scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value);
        }

        // A chart worth zero PUMBILITY holds a slot at no value. That is not merely wasteful:
        // the PUMBILITY page measures every projected gain against the pool's MINIMUM, and one
        // such row drives that floor to zero, which prints every suggestion's whole value as if
        // it displaced nothing. Four kinds rate zero — a stage break (StageBreakModifier), CO-OP
        // and half-double performance charts (ChartTypeModifiers), and anything below level 10
        // (DifficultyLevel.BaseRating) — and `Rank(s) > 0` is all four at once. Nothing
        // legitimate is caught: the worst grade multiplier is .4 on Phoenix and 1.08 on
        // Phoenix 2, and MinimumScore is 0 in both PUMBILITY configs.
        //
        // IsBroken stays an explicit filter rather than folding into the rank: the Phoenix
        // branch of Rank calls an overload that hardcodes isBroken false, so a stage break
        // ranks there at its unbroken value and the general rule would not see it.
        return (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && !s.IsBroken && (request.ChartType == null ||
                                                           charts[s.ChartId].Type == request.ChartType))
            .Select(s => (Score: s, Rank: Rank(s)))
            .Where(x => x.Rank > 0)
            .OrderByDescending(x => x.Rank)
            .Take(request.Count)
            .Select(x => x.Score)
            .ToArray();
    }

    public async Task Handle(RecalculateStatsCommand request, CancellationToken cancellationToken)
    {
        // The public recalc entry (admin tools, scheduled maintenance) — the session
        // pipeline goes through CaptureSessionStats, which needs the core's outputs. No change
        // set here, so no old scores, so no per-chart PUMBILITY split: an admin recalculation
        // already writes no highlight flags for the same reason.
        await RecalculateCore(request, null, cancellationToken);
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetTop50CompetitiveQuery request,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var count = request.ChartType == null ? 100 : 50;
        // Broken attempts never rate, the same rule RecalculateCore applies when it computes
        // the stored competitive level: a walkoff's partial score deflates small accounts'
        // averages, and a deep partial on an overrated chart would farm competitive level
        // without ever passing it. The query and the stored figure have to agree on that.
        return (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && !s.IsBroken && (request.ChartType == null ||
                                                           charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score!.Value,
                    charts[s.ChartId].Type))
            .Take(count).ToArray();
    }

    public async Task Handle(RecalculatePumbilityCommand request, CancellationToken cancellationToken)
    {
        var mix = request.Mix;
        var scores = (await _scores.GetPlayerScores(mix, new[] { request.UserId },
            request.chartIds,
            cancellationToken)).ToArray();
        var pumbility = ScoringConfiguration.PumbilityScoring(mix, true);
        var pumbilityPlus = ScoringConfiguration.PumbilityPlus;

        var charts = (await _charts.GetCharts(mix,
                chartIds: request.chartIds,
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        var ratings = scores.Select(s => new PhoenixRecordStats(s.ChartId,
            pumbility.GetScore(charts[s.ChartId], s.Score, s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken),
            pumbilityPlus.GetScore(charts[s.ChartId], s.Score, s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)));
        await _recordStats.UpdateScoreStats(mix, request.UserId, ratings, cancellationToken);
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        // New users start with a Phoenix stats row (default mix at release); other mixes'
        // rows appear the first time that mix's stats recompute.
        await _stats.SaveStats(MixEnum.Phoenix, context.Message.UserId,
            new PlayerStatsRecord(context.Message.UserId, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 1, 1),
            context.CancellationToken);
    }

    private async Task<SessionStatsResult> RecalculateCore(RecalculateStatsCommand request,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange>? changes,
        CancellationToken cancellationToken)
    {
        var mix = request.Mix;
        var oldStats = await _stats.GetStats(mix, request.UserId, cancellationToken);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, true);
        var charts =
            (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var recorded =
            (await _scores.GetBestScores(mix, request.UserId, cancellationToken)).ToArray();

        // Phoenix 2's formula prices the plate (and zeroes stage breaks); Phoenix keeps its
        // historical plate-blind rating byte-identical.
        double Rate(RecordedPhoenixScore s)
        {
            return mix == MixEnum.Phoenix2
                ? scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value,
                    s.Plate ?? PhoenixPlate.RoughGame, s.IsBroken)
                : scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value);
        }

        var scores = recorded
            .Where(s => s.Score != null)
            .Select(s => new ChartRating(s.ChartId, charts[s.ChartId].Type, Rate(s), s.Score!.Value, s.IsBroken))
            .ToArray();
        // Broken attempts never rate: a walkoff's partial score deflates small accounts'
        // competitive averages, and a deep partial on an overrated chart would farm
        // competitive level without ever passing it.
        var competitiveScores = recorded.Where(s => s.Score != null && !s.IsBroken)
            .Select(s => new ChartCompetitive(s.ChartId, charts[s.ChartId].Type,
                ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score!.Value,
                    charts[s.ChartId].Type),
                s.Score!.Value)).ToArray();

        var top50 = scores
            .Where(s => !s.IsBroken && s.Type != ChartType.CoOp)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var top50Singles = scores.Where(s => !s.IsBroken && s.Type == ChartType.Single)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var top50Doubles = scores.Where(s => !s.IsBroken && s.Type == ChartType.Double)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var coOps = scores.Where(s => !s.IsBroken && s.Type == ChartType.CoOp)
            .ToArray();
        var competitive =
            AvgOr0(competitiveScores.OrderByDescending(e => e.CompetitiveLevel).Take(100)
                .Select(s => s.CompetitiveLevel).ToArray());
        var competitiveSingles =
            AvgOr0(competitiveScores.Where(s => s.Type == ChartType.Single)
                .OrderByDescending(s => s.CompetitiveLevel)
                .Take(50).Select(s => ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score))
                .ToArray());
        var competitiveDoubles =
            AvgOr0(competitiveScores.Where(s => s.Type == ChartType.Double).OrderByDescending(s => s.CompetitiveLevel)
                .Take(50).Select(s => ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score))
                .ToArray());

        // Overall rating is the mixed top-50; Singles/Doubles below are the per-type top-50s.
        var skillPool = top50;
        var skillRating = (int)top50.Sum(s => s.Rating);

        var newStats = new PlayerStatsRecord(request.UserId,
            (int)scores.Where(s => !s.IsBroken).Sum(s => s.Rating),
            recorded.Any(r => !r.IsBroken) ? recorded.Where(r => !r.IsBroken).Max(r => charts[r.ChartId].Level) : 1,
            recorded.Count(r => !r.IsBroken),
            (int)coOps.Sum(s => s.Rating),
            (int)AverageOrDefault(coOps.Select(s => (int)s.Score), 0),
            skillRating,
            (int)AverageOrDefault(skillPool.Select(s => (int)s.Score), 0),
            AverageOrDefault(skillPool.Select(s => (int)charts[s.ChartId].Level), 1),
            (int)top50Singles.Sum(s => s.Rating),
            (int)AverageOrDefault(top50Singles.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50Singles.Select(s => (int)charts[s.ChartId].Level), 1),
            (int)top50Doubles.Sum(s => s.Rating),
            (int)AverageOrDefault(top50Doubles.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50Doubles.Select(s => (int)charts[s.ChartId].Level), 1),
            competitive,
            competitiveSingles,
            competitiveDoubles
        );

        newStats = await EstimateOfficialRanks(mix, newStats, cancellationToken);
        await _stats.SaveStats(mix, request.UserId, newStats, cancellationToken);
        var gains = PumbilityGains(request, changes, scores, recorded, charts, mix, scoring);
        var improvers = await FlagCompetitiveImprovers(request, oldStats, newStats, competitiveScores, charts,
            gains, cancellationToken);
        var milestones = await CaptureRatingMilestones(request, oldStats, newStats, cancellationToken);
        if (newStats.SkillRating > oldStats.SkillRating || newStats.SinglesRating > oldStats.SinglesRating ||
            newStats.DoublesRating > oldStats.DoublesRating || newStats.ClearCount > oldStats.ClearCount ||
            newStats.CoOpRating > oldStats.CoOpRating)
            await _bus.Publish(new PlayerRatingsImprovedEvent(request.UserId, oldStats.SkillRating,
                    oldStats.SinglesRating, oldStats.DoublesRating, newStats.SkillRating, newStats.SinglesRating,
                    newStats.DoublesRating, oldStats.CompetitiveLevel, newStats.CompetitiveLevel,
                    oldStats.SinglesCompetitiveLevel, newStats.SinglesCompetitiveLevel,
                    oldStats.DoublesCompetitiveLevel,
                    newStats.DoublesCompetitiveLevel, (int)coOps.Sum(s => s.Rating), recorded.Count(r => !r.IsBroken),
                    mix, request.SessionId),
                cancellationToken);
        await _bus.Publish(new PlayerStatsUpdatedEvent(request.UserId, newStats, mix),
            cancellationToken);
        await _mediator.Publish(new PlayerStatsUpdatedEvent(request.UserId, newStats, mix),
            cancellationToken);
        return new SessionStatsResult(milestones, improvers);
    }

    /// <summary>
    ///     A rank movement worth announcing: onto the board for the first time, or up it.
    ///     Falling off, or sliding down, mints nothing — the board moves under everyone every
    ///     week and a player who did nothing should not be told they lost ground.
    /// </summary>
    private void AddRankGain(List<PlayerMilestoneWrite> milestones, RecalculateStatsCommand request,
        MilestoneKind kind, int? oldRank, int? newRank, string boardName)
    {
        if (newRank == null) return;
        if (oldRank != null && newRank >= oldRank) return;
        milestones.Add(new PlayerMilestoneWrite(kind, request.SessionId, _dateTime.Now,
            oldRank, newRank.Value, Detail: boardName));
    }

    /// <summary>
    ///     Places the freshly computed pools on the official boards. We rank OUR number against
    ///     the last sealed board rather than reading a rank back from it — a mirrored rank only
    ///     moves when the sweep runs, so it could never reflect what the player just played.
    ///     The board is up to a week old while the pool is current, so the estimate leans
    ///     generous; <c>PumbilityBoardAsOf</c> is what lets the UI say so.
    ///     <para>
    ///         Phoenix publishes one combined board. Phoenix 2 publishes three, and its title
    ///         ladder gates on the per-type pools, which is why they are worth carrying there
    ///         and nowhere else.
    ///     </para>
    /// </summary>
    private async Task<PlayerStatsRecord> EstimateOfficialRanks(MixEnum mix, PlayerStatsRecord stats,
        CancellationToken cancellationToken)
    {
        try
        {
            var combined = await _officialBoards.GetPumbilityBoard(mix, OfficialPumbilityBoardNames.Combined,
                cancellationToken);
            if (combined == null) return stats;

            // The board reads in decimals, so the pool is compared to it whole. Narrowing to an
            // int here would rank a 17,195.42 pool as 17,195 and hand back the place of a player
            // it does not actually tie.
            int? RankOn(OfficialBoardReading? board, double pool)
            {
                if (board == null) return null;
                var value = (decimal)pool;
                return board.IsRanked(value) ? board.PlaceFor(value) : null;
            }

            var singles = mix == MixEnum.Phoenix2
                ? await _officialBoards.GetPumbilityBoard(mix, OfficialPumbilityBoardNames.Singles, cancellationToken)
                : null;
            var doubles = mix == MixEnum.Phoenix2
                ? await _officialBoards.GetPumbilityBoard(mix, OfficialPumbilityBoardNames.Doubles, cancellationToken)
                : null;

            return stats with
            {
                EstimatedPumbilityRank = RankOn(combined, stats.SkillRating),
                EstimatedSinglesPumbilityRank = RankOn(singles, stats.SinglesRating),
                EstimatedDoublesPumbilityRank = RankOn(doubles, stats.DoublesRating),
                PumbilityBoardAsOf = combined.AsOf
            };
        }
        catch (Exception ex)
        {
            // The mirror sweeps weekly and lives behind a login — a board it cannot serve
            // costs the estimate, never the stats it rides along with.
            _logger.LogError(ex, "Official PUMBILITY board read failed ({Mix}) — ranks left unchanged", mix);
            return stats;
        }
    }

    private double AverageOrDefault(IEnumerable<int> values, double def)
    {
        var enumerable = values as int[] ?? values.ToArray();
        return enumerable.Length > 0 ? enumerable.Average() : def;
    }

    private static double AvgOr0(double[] charts)
    {
        return charts.Length > 0 ? charts.Average() : 0;
    }

    // Pumbility and Singles/Doubles competitive gains become timestamped milestones —
    // neither was persisted with a timestamp before this table. Combined competitive is
    // deliberately never a milestone (S and D don't compare). PUMBILITY floors at any
    // gain — even +1 (owner: "which happens").
    private async Task<IReadOnlyList<PlayerMilestoneRecord>> CaptureRatingMilestones(
        RecalculateStatsCommand request, PlayerStatsRecord oldStats, PlayerStatsRecord newStats,
        CancellationToken cancellationToken)
    {
        var milestones = new List<PlayerMilestoneWrite>();
        if (newStats.SkillRating > oldStats.SkillRating)
            milestones.Add(new PlayerMilestoneWrite(MilestoneKind.PumbilityGain, request.SessionId, _dateTime.Now,
                oldStats.SkillRating, newStats.SkillRating));
        // Phoenix 2's separate Singles/Doubles PUMBILITY pools mint their own milestones —
        // the P2 title ladder gates on them. Phoenix stays total-only: its S/D ratings
        // exist too, but pre-P2 sessions never minted them and shouldn't start now.
        if (request.Mix == MixEnum.Phoenix2)
        {
            if (newStats.SinglesRating > oldStats.SinglesRating)
                milestones.Add(new PlayerMilestoneWrite(MilestoneKind.SinglesPumbilityGain, request.SessionId,
                    _dateTime.Now, oldStats.SinglesRating, newStats.SinglesRating));
            if (newStats.DoublesRating > oldStats.DoublesRating)
                milestones.Add(new PlayerMilestoneWrite(MilestoneKind.DoublesPumbilityGain, request.SessionId,
                    _dateTime.Now, oldStats.DoublesRating, newStats.DoublesRating));
        }

        // Official rank improves DOWNWARD, and only an improvement mints. An undo republishes
        // an empty score event through this same path, which recomputes the pools lower and
        // would otherwise announce the seat it just cost the player.
        AddRankGain(milestones, request, MilestoneKind.OfficialPumbilityRank,
            oldStats.EstimatedPumbilityRank, newStats.EstimatedPumbilityRank,
            OfficialPumbilityBoardNames.Combined);
        if (request.Mix == MixEnum.Phoenix2)
        {
            AddRankGain(milestones, request, MilestoneKind.OfficialPumbilityRank,
                oldStats.EstimatedSinglesPumbilityRank, newStats.EstimatedSinglesPumbilityRank,
                OfficialPumbilityBoardNames.Singles);
            AddRankGain(milestones, request, MilestoneKind.OfficialPumbilityRank,
                oldStats.EstimatedDoublesPumbilityRank, newStats.EstimatedDoublesPumbilityRank,
                OfficialPumbilityBoardNames.Doubles);
        }

        if (CompetitiveGained(oldStats.SinglesCompetitiveLevel, newStats.SinglesCompetitiveLevel))
            milestones.Add(new PlayerMilestoneWrite(MilestoneKind.SinglesCompetitiveGain, request.SessionId,
                _dateTime.Now, oldStats.SinglesCompetitiveLevel, newStats.SinglesCompetitiveLevel));
        if (CompetitiveGained(oldStats.DoublesCompetitiveLevel, newStats.DoublesCompetitiveLevel))
            milestones.Add(new PlayerMilestoneWrite(MilestoneKind.DoublesCompetitiveGain, request.SessionId,
                _dateTime.Now, oldStats.DoublesCompetitiveLevel, newStats.DoublesCompetitiveLevel));

        if (milestones.Count > 0)
            await _milestones.Append(request.Mix, request.UserId, milestones, cancellationToken);
        return milestones
            .Select(m => new PlayerMilestoneRecord(m.Kind, m.SessionId, m.OccurredAt, m.OldValue, m.NewValue,
                m.Title, m.Detail))
            .ToArray();
    }

    // The revision-2 noise floor (owner call): competitive micro-gains under a
    // hundredth are recomputation noise, not milestones — the +0.002 lines were the
    // poster child of the old message dump.
    private static bool CompetitiveGained(double oldLevel, double newLevel)
    {
        return newLevel - oldLevel >= 0.01;
    }

    // The CompetitiveImprover highlight flag: when a batch raised the Singles or Doubles
    // competitive level (never combined — S and D don't compare), flag the changed scores
    // on the improved side strong enough to pull the average up (Fung score at or above
    // the OLD level). Written here because this saga owns the old-vs-new numbers; the
    // orchestrator merges the returned ids into the snapshot event's flags, so the ⬆
    // badge rides the Discord card instead of trailing it.
    //
    // The flagged rows carry that OLD level with them. It is the number the comparison was
    // made against, it is per-batch, and nothing downstream can recover it — the stats row
    // remembers only where the session ended. The score's own competitive level stays a pure
    // function, so storing the one number is what lets a row read "23.6 (+0.4)".
    //
    // PUMBILITY gains ride the same write. They are not tied to the improver flag and reach
    // charts it never touches, so any chart with a gain and no flag still gets a row.
    private async Task<IReadOnlyList<Guid>> FlagCompetitiveImprovers(RecalculateStatsCommand request,
        PlayerStatsRecord oldStats, PlayerStatsRecord newStats, ChartCompetitive[] competitiveScores,
        Dictionary<Guid, Chart> charts, IReadOnlyDictionary<Guid, int> gains,
        CancellationToken cancellationToken)
    {
        if (request.ChangedChartIds == null || request.SessionId == null) return Array.Empty<Guid>();
        var changed = request.ChangedChartIds.ToHashSet();
        var improvers = new HashSet<Guid>();
        var baselines = new Dictionary<Guid, double>();
        foreach (var (type, oldLevel, improved) in new[]
                 {
                     (ChartType.Single, oldStats.SinglesCompetitiveLevel,
                         newStats.SinglesCompetitiveLevel > oldStats.SinglesCompetitiveLevel),
                     (ChartType.Double, oldStats.DoublesCompetitiveLevel,
                         newStats.DoublesCompetitiveLevel > oldStats.DoublesCompetitiveLevel)
                 })
        {
            if (!improved) continue;
            foreach (var s in competitiveScores.Where(s =>
                         s.Type == type && changed.Contains(s.ChartId) && s.CompetitiveLevel >= oldLevel))
            {
                improvers.Add(s.ChartId);
                baselines[s.ChartId] = oldLevel;
            }
        }

        var writes = improvers.Select(chartId => new ScoreHighlightWrite(chartId, request.SessionId,
                _dateTime.Now, HighlightFlags.CompetitiveImprover, charts[chartId].Level, null,
                new HighlightDetail(CompetitiveBaseline: baselines[chartId],
                    PumbilityGain: gains.GetValueOrDefault(chartId) is var g && g > 0 ? g : null)))
            .Concat(gains.Where(g => !improvers.Contains(g.Key))
                .Select(g => new ScoreHighlightWrite(g.Key, request.SessionId, _dateTime.Now,
                    HighlightFlags.None, charts[g.Key].Level, null,
                    new HighlightDetail(PumbilityGain: g.Value))))
            .ToArray();

        if (writes.Length > 0)
            await _highlights.UpsertFlags(request.Mix, request.UserId, writes, cancellationToken);
        return improvers.ToArray();
    }

    /// <summary>
    ///     What each changed chart added to the combined PUMBILITY pool. Needs the batch's old
    ///     scores, so an admin recalculation (no change set) reports nothing rather than
    ///     pretending every chart gained its whole value.
    ///     <para>
    ///         ⚠ The old value is priced with the chart's CURRENT plate. On Phoenix that is exact,
    ///         because its formula never reads the plate. On Phoenix 2 it is exact unless the
    ///         plate improved in the same play that raised the score, where the old side is
    ///         priced a little high and the gain reads a little low. Carrying the old plate
    ///         through the event is what would close that, and the event does not have it.
    ///     </para>
    /// </summary>
    private static IReadOnlyDictionary<Guid, int> PumbilityGains(RecalculateStatsCommand request,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange>? changes, ChartRating[] scores,
        RecordedPhoenixScore[] recorded, Dictionary<Guid, Chart> charts, MixEnum mix,
        ScoringConfiguration scoring)
    {
        if (changes == null || changes.Count == 0 || request.SessionId == null)
            return new Dictionary<Guid, int>();

        // A new pass held NO seat, whatever score preceded it. The pool counts non-broken
        // scores only, so a chart whose prior best was a stage break was not in it — and
        // pricing that break as though it were a clean pass puts the chart in the old pool at
        // nearly its new value, which collapses a real entry down to the score difference.
        // That is how a chart entering at #7 reported "+2".
        var prior = changes.GroupBy(c => c.ChartId)
            .ToDictionary(g => g.Key, g => g.Any(c => c.IsNewPass)
                ? null
                : g.Select(c => c.OldScore).Max());
        var bests = recorded.ToDictionary(r => r.ChartId);

        double PriceAt(Guid chartId, int score)
        {
            var chart = charts[chartId];
            if (mix != MixEnum.Phoenix2)
                return scoring.GetScore(chart.Type, chart.Level, PhoenixScore.From(score));

            var plate = bests.TryGetValue(chartId, out var best) ? best.Plate : null;
            return scoring.GetScore(chart.Type, chart.Level, PhoenixScore.From(score),
                plate ?? PhoenixPlate.RoughGame, false);
        }

        // The pool the ceremony band headlines: non-broken, no CO-OP, top 50 — the same set
        // SkillRating sums, so the split adds up to the movement the band already reports.
        var priced = scores
            .Where(s => !s.IsBroken && s.Type != ChartType.CoOp)
            .Select(s => new PumbilityAttribution.Priced(s.ChartId,
                !prior.TryGetValue(s.ChartId, out var old) ? s.Rating
                : old == null ? null
                : PriceAt(s.ChartId, old.Value),
                s.Rating))
            .ToArray();

        return PumbilityAttribution.GainsPerChart(priced, 50);
    }
}
