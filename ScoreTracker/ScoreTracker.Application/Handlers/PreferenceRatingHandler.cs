using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class PreferenceRatingHandler :
        IRequestHandler<UpdatePreferenceRatingCommand, ChartPreferenceRatingRecord>,
        IRequestHandler<GetAllPreferenceRatingsQuery, IEnumerable<ChartPreferenceRatingRecord>>,
        IRequestHandler<GetUserPreferenceRatingsQuery, IEnumerable<UserRatingsRecord>>
    {
        private readonly IChartPreferenceRepository _preferences;
        private readonly ICurrentUserAccessor _currentUser;

        public PreferenceRatingHandler(IChartPreferenceRepository preferences, ICurrentUserAccessor currentUser)
        {
            _preferences = preferences;
            _currentUser = currentUser;
        }

        public async Task<ChartPreferenceRatingRecord> Handle(UpdatePreferenceRatingCommand request,
            CancellationToken cancellationToken)
        {
            await _preferences.SaveRating(request.Mix, _currentUser.User.Id, request.ChartId, request.Rating,
                cancellationToken);
            var ratings = (await _preferences.GetRatingsForChart(request.Mix, request.ChartId, cancellationToken))
                .ToArray();
            var average = ratings.Average(r => (decimal)r);
            var count = ratings.Count();
            await _preferences.SetAverageRating(request.Mix, request.ChartId, average, count, cancellationToken);
            return new ChartPreferenceRatingRecord(request.ChartId, average, count);
        }

        public async Task<IEnumerable<ChartPreferenceRatingRecord>> Handle(GetAllPreferenceRatingsQuery request,
            CancellationToken cancellationToken)
        {
            return await _preferences.GetPreferenceRatings(request.Mix, cancellationToken);
        }

        public async Task<IEnumerable<UserRatingsRecord>> Handle(GetUserPreferenceRatingsQuery request,
            CancellationToken cancellationToken)
        {
            return await _preferences.GetUserRatings(request.Mix, _currentUser.User.Id, cancellationToken);
        }
    }
}
