using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class RateCoOpDifficultyHandler : IRequestHandler<RateCoOpDifficultyCommand, CoOpRating>
    {
        private readonly IChartRepository _chartRepository;
        private readonly IChartDifficultyRatingRepository _ratingRepository;
        private readonly ICurrentUserAccessor _currentUserAccessor;

        public RateCoOpDifficultyHandler(IChartRepository chartRepository,
            IChartDifficultyRatingRepository ratingRepository,
            ICurrentUserAccessor currentUserAccessor)
        {
            _chartRepository = chartRepository;
            _ratingRepository = ratingRepository;
            _currentUserAccessor = currentUserAccessor;
        }

        public async Task<CoOpRating?> Handle(RateCoOpDifficultyCommand request, CancellationToken cancellationToken)
        {
            var chart = await _chartRepository.GetChart(request.mix, request.ChartId, cancellationToken);
            if (chart.Type != ChartType.CoOp)
            {
                throw new ArgumentException($"Chart {chart.Song.Name} {chart.DifficultyString} is not a CoOp",
                    nameof(request.ChartId));
            }

            if (request.Ratings != null && chart.PlayerCount != request.Ratings.Count)
            {
                throw new ArgumentException(
                    $"Player count is mismatched, registered {request.Ratings.Count} for chart {chart.Song.Name} {chart.DifficultyString} which has {chart.PlayerCount} players");
            }

            await _ratingRepository.SetMyCoOpRating(_currentUserAccessor.User.Id, request.ChartId, request.Ratings,
                cancellationToken);

            var ratings = await _ratingRepository.GetCoOpRatings(request.ChartId, cancellationToken);

            if (ratings.Any() && ratings[1].Any())
            {
                var newRating = new CoOpRating(request.ChartId, ratings[1].Count(),
                    ratings.ToDictionary(r => r.Key, r => (DifficultyLevel)(int)Math.Round(r.Value.Average(l => l))));

                await _ratingRepository.SaveCoOpRating(newRating, cancellationToken);

                return newRating;
            }

            await _ratingRepository.ClearCoOpRating(request.ChartId, cancellationToken);
            return null;
        }
    }
}