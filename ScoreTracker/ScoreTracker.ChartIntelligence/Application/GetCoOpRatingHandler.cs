using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Application
{
    internal sealed class GetCoOpRatingHandler : IRequestHandler<GetCoOpRatingQuery, CoOpRating?>
    {
        private readonly IChartDifficultyRatingRepository _ratings;

        public GetCoOpRatingHandler(IChartDifficultyRatingRepository ratings)
        {
            _ratings = ratings;
        }

        public async Task<CoOpRating?> Handle(GetCoOpRatingQuery request, CancellationToken cancellationToken)
        {
            return await _ratings.GetCoOpRating(request.ChartId, cancellationToken);
        }
    }
}
