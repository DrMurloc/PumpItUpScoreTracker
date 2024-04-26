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

    public static ScoringConfiguration Scoring => CreateScoring();

    private static ScoringConfiguration CreateScoring()
    {
        var result = new ScoringConfiguration
        {
            ContinuousLetterGradeScale = true
        };
        result.AdjustToTime = false;
        result.PgLetterGradeModifier = 1.6;
        result.LevelRatings[1] = 10;
        result.LevelRatings[2] = 20;
        result.LevelRatings[3] = 30;
        result.LevelRatings[4] = 40;
        result.LevelRatings[5] = 50;
        result.LevelRatings[6] = 60;
        result.LevelRatings[7] = 70;
        result.LevelRatings[8] = 80;
        result.LevelRatings[9] = 90;
        result.ChartTypeModifiers[ChartType.CoOp] = 1.0;
        return result;
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

    private sealed record ChartRating(Guid ChartId, ChartType Type, Rating Rating, PhoenixScore Score)
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

        return (await _scores.GetRecordedScores(request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
                                            charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                Scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level, s.Score!.Value))
            .Take(50).ToArray();
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        await _stats.SaveStats(context.Message.UserId,
            new PlayerStatsRecord(0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 1, 1), context.CancellationToken);
    }

    public sealed record RecalculateStats(Guid UserId) : IRequest
    {
    }

    public async Task Handle(RecalculateStats request, CancellationToken cancellationToken)
    {
        var oldStats = await _stats.GetStats(request.UserId, cancellationToken);

        var charts = (await _charts.GetCharts(MixEnum.Phoenix)).ToDictionary(c => c.Id);
        var recorded =
            (await _scores.GetRecordedScores(request.UserId, cancellationToken)).ToArray();
        var scores = recorded
            .Where(s => s.Score != null)
            .Select(s => new ChartRating(s.ChartId, charts[s.ChartId].Type,
                Scoring.GetScore(charts[s.ChartId].Type, charts[s.ChartId].Level,
                    s.Score!.Value), s.Score!.Value))
            .ToArray();
        var competitiveScores = recorded.Where(s => s.Score != null)
            .Select(s => new ChartCompetitive(s.ChartId, charts[s.ChartId].Type,
                ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score!.Value),
                s.Score!.Value)).ToArray();

        var top50 = scores
            .Where(s => s.Type != ChartType.CoOp)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var top50Singles = scores.Where(s => s.Type == ChartType.Single)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var top50Doubles = scores.Where(s => s.Type == ChartType.Double)
            .OrderByDescending(s => s.Rating)
            .Take(50).ToArray();

        var coOps = scores.Where(s => s.Type == ChartType.CoOp)
            .ToArray();
        var competitive =
            competitiveScores.OrderByDescending(e => e.CompetitiveLevel).Take(50).Sum(s => s.CompetitiveLevel) / 50.0;
        var competitiveSingles =
            competitiveScores.Where(s => s.Type == ChartType.Single)
                .OrderByDescending(s => s.CompetitiveLevel)
                .Take(50).Sum(s => ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score)) / 50.0;
        var competitiveDoubles =
            competitiveScores.Where(s => s.Type == ChartType.Double).OrderByDescending(s => s.CompetitiveLevel)
                .Take(50).Sum(s => ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score)) / 50.0;

        var newStats = new PlayerStatsRecord(scores.Sum(s => s.Rating),
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
            newStats.DoublesRating > oldStats.DoublesRating)
            await _bus.Publish(new PlayerRatingsImprovedEvent(request.UserId, oldStats.SkillRating,
                oldStats.SinglesRating, oldStats.DoublesRating, newStats.SkillRating, newStats.SinglesRating,
                newStats.DoublesRating, oldStats.CompetitiveLevel, newStats.CompetitiveLevel,
                oldStats.SinglesCompetitiveLevel, newStats.SinglesCompetitiveLevel, oldStats.DoublesCompetitiveLevel,
                newStats.DoublesCompetitiveLevel));
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

        return (await _scores.GetRecordedScores(request.UserId, cancellationToken))
            .Where(s => charts[s.ChartId].Type != ChartType.CoOp)
            .Where(s => s.Score != null && (request.ChartType == null ||
                                            charts[s.ChartId].Type == request.ChartType))
            .OrderByDescending(s =>
                ScoringConfiguration.CalculateFungScore(charts[s.ChartId].Level, s.Score!.Value))
            .Take(50).ToArray();
    }
}