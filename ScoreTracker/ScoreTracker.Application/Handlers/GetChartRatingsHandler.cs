using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetChartRatingsHandler : IRequestHandler<GetChartRatingsQuery, IEnumerable<ChartDifficultyRatingRecord>>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IChartDifficultyRatingRepository _ratings;

    public GetChartRatingsHandler(IChartDifficultyRatingRepository ratings, ICurrentUserAccessor currentUser,
        IChartRepository charts)
    {
        _ratings = ratings;
        _currentUser = currentUser;
        _charts = charts;
    }

    public async Task<IEnumerable<ChartDifficultyRatingRecord>> Handle(GetChartRatingsQuery request,
        CancellationToken cancellationToken)
    {
        var result = (await _ratings.GetAllChartRatedDifficulties(cancellationToken)).ToArray();
        if (request.Level != null || request.Type != null)
        {
            var charts = (await _charts.GetCharts(null, request.Level, request.Type, cancellationToken))
                .Select(c => c.Id)
                .ToHashSet();
            result = result.Where(r => charts.Contains(r.ChartId)).ToArray();
        }

        if (!_currentUser.IsLoggedIn) return result;

        var myRatings =
            (await _ratings.GetRatingsByUser(_currentUser.User.Id, cancellationToken)).ToDictionary(r => r.ChartId,
                r => r.Rating);

        foreach (var r in result)
            if (myRatings.ContainsKey(r.ChartId))
                r.MyRating = myRatings[r.ChartId];

        return result;
    }
}