using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class
        GetMyCoOpRatingHandler : IRequestHandler<GetMyCoOpRatingQuery, IDictionary<int, DifficultyLevel>?>
    {
        private readonly IChartDifficultyRatingRepository _ratings;
        private readonly ICurrentUserAccessor _currentUser;

        public GetMyCoOpRatingHandler(IChartDifficultyRatingRepository ratings,
            ICurrentUserAccessor currentUser)
        {
            _ratings = ratings;
            _currentUser = currentUser;
        }

        public async Task<IDictionary<int, DifficultyLevel>?> Handle(GetMyCoOpRatingQuery request,
            CancellationToken cancellationToken)
        {
            return await _ratings.GetMyCoOpRating(_currentUser.User.Id, request.ChartId, cancellationToken);
        }
    }
}