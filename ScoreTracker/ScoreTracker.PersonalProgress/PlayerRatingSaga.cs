using MassTransit;
using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress.Queries;

namespace ScoreTracker.PersonalProgress;

public sealed class PlayerRatingSaga : IConsumer<PlayerScoreUpdatedEvent>,
    IRequestHandler<GetTop50ForPlayerQuery, IEnumerable<RecordedPhoenixScore>>,
    IRequestHandler<GetTop50CompetitiveQuery, IEnumerable<RecordedPhoenixScore>>,
    IRequestHandler<PlayerRatingSaga.RecalculateStats>,
    IConsumer<UserCreatedEvent>
{
    private readonly IPhoenixRecordRepository _scores;
    private readonly IChartRepository _charts;
    private readonly IPlayerStatsRepository _stats;
    private readonly IBus _bus;
    private readonly IMediator _mediator;

    public PlayerRatingSaga(IPhoenixRecordRepository scores, IChartRepository charts, IPlayerStatsRepository stats,
        IBus bus, IMediator mediator)
    {
        _scores = scores;
        _charts = charts;
        _stats = stats;
        _bus = bus;
        _mediator = mediator;
    }


    public async Task Consume(ConsumeContext<PlayerScoreUpdatedEvent> context)
    {
        await Handle(new RecalculateStats(context.Message.UserId), context.CancellationToken);
    }

    private double AverageOrDefault(IEnumerable<int> values, double def)
    {
        var enumerable = values as int[] ?? values.ToArray();
        return enumerable.Any() ? enumerable.Average() : def;
    }

    private sealed record ChartRating(Guid ChartId, ChartType Type, Rating Rating, PhoenixScore Score, bool IsBroken)
    {
    }

    private sealed record ChartCompetitive(Guid ChartId, ChartType Type, double CompetitiveLevel, PhoenixScore Score)
    {
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetTop50ForPlayerQuery request,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(false);
        return (await _scores.GetRecordedScores(request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
                                            charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value))
            .Take(request.Count).ToArray();
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        await _stats.SaveStats(context.Message.UserId,
            new PlayerStatsRecord(context.Message.UserId, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 1, 1),
            context.CancellationToken);
    }

    public sealed record RecalculateStats(Guid UserId) : IRequest
    {
    }

    public async Task Handle(RecalculateStats request, CancellationToken cancellationToken)
    {
        var oldStats = await _stats.GetStats(request.UserId, cancellationToken);
        var scoring = ScoringConfiguration.PumbilityScoring(true);
        var charts =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var recorded =
            (await _scores.GetRecordedScores(request.UserId, cancellationToken)).ToArray();
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

        var newStats = new PlayerStatsRecord(request.UserId, scores.Sum(s => s.Rating),
            recorded.Any(r => !r.IsBroken) ? recorded.Where(r => !r.IsBroken).Max(r => charts[r.ChartId].Level) : 1,
            recorded.Count(r => !r.IsBroken),
            coOps.Sum(s => s.Rating),
            (int)AverageOrDefault(coOps.Select(s => (int)s.Score), 0),
            top50.Sum(s => s.Rating),
            (int)AverageOrDefault(top50.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50.Select(s => (int)charts[s.ChartId].Level), 1),
            top50Singles.Sum(s => s.Rating),
            (int)AverageOrDefault(top50Singles.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50Singles.Select(s => (int)charts[s.ChartId].Level), 1),
            top50Doubles.Sum(s => s.Rating),
            (int)AverageOrDefault(top50Doubles.Select(s => (int)s.Score), 0),
            AverageOrDefault(top50Doubles.Select(s => (int)charts[s.ChartId].Level), 1),
            competitive,
            competitiveSingles,
            competitiveDoubles
        );

        await _stats.SaveStats(request.UserId, newStats, cancellationToken);
        if (newStats.SkillRating > oldStats.SkillRating || newStats.SinglesRating > oldStats.SinglesRating ||
            newStats.DoublesRating > oldStats.DoublesRating || newStats.ClearCount > oldStats.ClearCount ||
            newStats.CoOpRating > oldStats.CoOpRating)
            await _bus.Publish(new PlayerRatingsImprovedEvent(request.UserId, oldStats.SkillRating,
                    oldStats.SinglesRating, oldStats.DoublesRating, newStats.SkillRating, newStats.SinglesRating,
                    newStats.DoublesRating, oldStats.CompetitiveLevel, newStats.CompetitiveLevel,
                    oldStats.SinglesCompetitiveLevel, newStats.SinglesCompetitiveLevel,
                    oldStats.DoublesCompetitiveLevel,
                    newStats.DoublesCompetitiveLevel, coOps.Sum(s => s.Rating), recorded.Count(r => !r.IsBroken)),
                cancellationToken);
        await _bus.Publish(new PlayerStatsUpdatedEvent(request.UserId, newStats),
            cancellationToken);
        await _mediator.Publish(new PlayerStatsUpdatedEvent(request.UserId, newStats),
            cancellationToken);
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetTop50CompetitiveQuery request,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var count = request.ChartType == null ? 100 : 50;
        return (await _scores.GetRecordedScores(request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
                                            charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score!.Value,
                    charts[s.ChartId].Type))
            .Take(count).ToArray();
    }

    private static double AvgOr0(double[] charts)
    {
        return charts.Any() ? charts.Average() : 0;
    }
}
