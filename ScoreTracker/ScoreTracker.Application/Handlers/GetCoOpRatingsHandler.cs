using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetCoOpRatingsHandler : IRequestHandler<GetCoOpRatingsQuery, IEnumerable<CoOpRating>>
    {
        private readonly IChartDifficultyRatingRepository _ratings;

        public GetCoOpRatingsHandler(IChartDifficultyRatingRepository ratings)
        {
            _ratings = ratings;
        }

        public async Task<IEnumerable<CoOpRating>> Handle(GetCoOpRatingsQuery request,
            CancellationToken cancellationToken)
            => await _ratings.GetAllCoOpRatings(cancellationToken);
    }
}