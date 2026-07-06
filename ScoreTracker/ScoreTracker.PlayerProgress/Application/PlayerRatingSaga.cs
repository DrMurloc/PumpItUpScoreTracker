using MassTransit;
using MediatR;
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
    public sealed record CaptureSessionStats(
        Guid UserId, MixEnum Mix, IReadOnlyList<Guid> ChangedChartIds, Guid? SessionId)
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

    public PlayerRatingSaga(IScoreReader scores, IPhoenixRecordStatsRepository recordStats,
        IChartRepository charts, IPlayerStatsRepository stats, IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones, IDateTimeOffsetAccessor dateTime, IBus bus, IMediator mediator)
    {
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
            request.ChangedChartIds, request.SessionId), cancellationToken);
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
        var scoring = ScoringConfiguration.PumbilityScoring(false);
        return (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
                                            charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value))
            .Take(request.Count).ToArray();
    }

    public async Task Handle(RecalculateStatsCommand request, CancellationToken cancellationToken)
    {
        // The public recalc entry (admin tools, scheduled maintenance) — the session
        // pipeline goes through CaptureSessionStats, which needs the core's outputs.
        await RecalculateCore(request, cancellationToken);
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetTop50CompetitiveQuery request,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var count = request.ChartType == null ? 100 : 50;
        return (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
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
        var pumbility = ScoringConfiguration.PumbilityScoring(true);
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
        CancellationToken cancellationToken)
    {
        var mix = request.Mix;
        var oldStats = await _stats.GetStats(mix, request.UserId, cancellationToken);
        var scoring = ScoringConfiguration.PumbilityScoring(true);
        var charts =
            (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var recorded =
            (await _scores.GetBestScores(mix, request.UserId, cancellationToken)).ToArray();
        var scores = recorded
            .Where(s => s.Score != null)
            .Select(s => new ChartRating(s.ChartId, charts[s.ChartId].Type,
                scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level,
                    s.Score!.Value), s.Score!.Value, s.IsBroken))
            .ToArray();
        var competitiveScores = recorded.Where(s => s.Score != null)
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

        var coOps = scores.Where(s => s.Type == ChartType.CoOp)
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

        var newStats = new PlayerStatsRecord(request.UserId, (int)scores.Sum(s => s.Rating),
            recorded.Any(r => !r.IsBroken) ? recorded.Where(r => !r.IsBroken).Max(r => charts[r.ChartId].Level) : 1,
            recorded.Count(r => !r.IsBroken),
            (int)coOps.Sum(s => s.Rating),
            (int)AverageOrDefault(coOps.Select(s => (int)s.Score), 0),
            (int)top50.Sum(s => s.Rating),
            (int)AverageOrDefault(top50.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50.Select(s => (int)charts[s.ChartId].Level), 1),
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

        await _stats.SaveStats(mix, request.UserId, newStats, cancellationToken);
        var improvers = await FlagCompetitiveImprovers(request, oldStats, newStats, competitiveScores, charts,
            cancellationToken);
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
    private async Task<IReadOnlyList<Guid>> FlagCompetitiveImprovers(RecalculateStatsCommand request,
        PlayerStatsRecord oldStats, PlayerStatsRecord newStats, ChartCompetitive[] competitiveScores,
        Dictionary<Guid, Chart> charts, CancellationToken cancellationToken)
    {
        if (request.ChangedChartIds == null || request.SessionId == null) return Array.Empty<Guid>();
        var changed = request.ChangedChartIds.ToHashSet();
        var writes = new List<ScoreHighlightWrite>();
        foreach (var (type, oldLevel, improved) in new[]
                 {
                     (ChartType.Single, oldStats.SinglesCompetitiveLevel,
                         newStats.SinglesCompetitiveLevel > oldStats.SinglesCompetitiveLevel),
                     (ChartType.Double, oldStats.DoublesCompetitiveLevel,
                         newStats.DoublesCompetitiveLevel > oldStats.DoublesCompetitiveLevel)
                 })
        {
            if (!improved) continue;
            writes.AddRange(competitiveScores
                .Where(s => s.Type == type && changed.Contains(s.ChartId) && s.CompetitiveLevel >= oldLevel)
                .Select(s => new ScoreHighlightWrite(s.ChartId, request.SessionId, _dateTime.Now,
                    HighlightFlags.CompetitiveImprover, charts[s.ChartId].Level, null)));
        }

        if (writes.Count > 0)
            await _highlights.UpsertFlags(request.Mix, request.UserId, writes, cancellationToken);
        return writes.Select(w => w.ChartId).Distinct().ToArray();
    }
}
