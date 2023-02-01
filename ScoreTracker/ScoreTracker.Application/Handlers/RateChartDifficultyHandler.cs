using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class RateChartDifficultyHandler : IRequestHandler<RateChartDifficultyCommand, double>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IChartDifficultyRatingRepository _difficultyRatings;

    public RateChartDifficultyHandler(IChartDifficultyRatingRepository difficultyRatings,
        ICurrentUserAccessor currentUser, IChartRepository charts)
    {
        _difficultyRatings = difficultyRatings;
        _currentUser = currentUser;
        _charts = charts;
    }

    public async Task<double> Handle(RateChartDifficultyCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        await _difficultyRatings.RateChart(request.ChartId, userId, request.Rating, cancellationToken);

        var chart = await _charts.GetChart(request.ChartId, cancellationToken);

        var ratings = (await _difficultyRatings.GetRatings(request.ChartId, cancellationToken)).ToArray();

        var baseDifficulty = (int)chart.Level + .5;

        var average = ratings.Average(rating => baseDifficulty + rating.GetAdjustment());

        await _difficultyRatings.SetAdjustedDifficulty(request.ChartId, average, ratings.Length, cancellationToken);

        return average;
    }
}