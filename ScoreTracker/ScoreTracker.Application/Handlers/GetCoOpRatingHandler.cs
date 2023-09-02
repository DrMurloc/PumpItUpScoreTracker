using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetCoOpRatingHandler : IRequestHandler<GetCoOpRatingQuery, CoOpRating?>
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