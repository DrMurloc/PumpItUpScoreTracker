using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Application
{
    internal sealed class GetCoOpRatingsHandler : IRequestHandler<GetCoOpRatingsQuery, IEnumerable<CoOpRating>>
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
