using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    ReCalculateChartRatingHandler : IRequestHandler<ReCalculateChartRatingCommand, ChartDifficultyRatingRecord>
{
    private readonly IChartRepository _charts;
    private readonly IChartDifficultyRatingRepository _difficultyRatings;

    public ReCalculateChartRatingHandler(IChartDifficultyRatingRepository difficultyRatings,
        IChartRepository charts)
    {
        _difficultyRatings = difficultyRatings;
        _charts = charts;
    }

    public async Task<ChartDifficultyRatingRecord> Handle(ReCalculateChartRatingCommand request,
        CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(request.Mix, request.ChartId, cancellationToken);

        var ratings = (await _difficultyRatings.GetRatings(request.Mix, request.ChartId, cancellationToken))
            .ToArray();

        var baseDifficulty = (int)chart.Level + .5;
        if (!ratings.Any())
        {
            await _difficultyRatings.ClearAdjustedDifficulty(request.Mix, request.ChartId, cancellationToken);
            return new ChartDifficultyRatingRecord(request.ChartId, baseDifficulty, 0, 0);
        }

        var average = ratings.Average(rating => baseDifficulty + rating.GetAdjustment());

        var standardDeviation =
            Math.Sqrt(ratings.Average(r => Math.Pow(baseDifficulty + r.GetAdjustment() - average, 2)));

        await _difficultyRatings.SetAdjustedDifficulty(request.Mix, request.ChartId, average, ratings.Length,
            standardDeviation,
            cancellationToken);

        return new ChartDifficultyRatingRecord(request.ChartId, baseDifficulty, ratings.Length, standardDeviation);
    }
}