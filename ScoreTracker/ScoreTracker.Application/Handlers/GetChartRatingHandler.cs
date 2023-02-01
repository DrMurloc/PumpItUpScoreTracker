using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetChartRatingHandler : IRequestHandler<GetChartRatingQuery, ChartDifficultyRatingRecord?>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IChartDifficultyRatingRepository _ratings;

    public GetChartRatingHandler(IChartDifficultyRatingRepository ratings, ICurrentUserAccessor currentUserAccessor)
    {
        _ratings = ratings;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ChartDifficultyRatingRecord?> Handle(GetChartRatingQuery request,
        CancellationToken cancellationToken)
    {
        var rating = await _ratings.GetChartRatedDifficulty(request.ChartId, cancellationToken);
        if (!_currentUserAccessor.IsLoggedIn || rating == null) return rating;

        var myRating = await _ratings.GetRating(request.ChartId, _currentUserAccessor.User.Id, cancellationToken);
        rating.MyRating = myRating;

        return rating;
    }
}