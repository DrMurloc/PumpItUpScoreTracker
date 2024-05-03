using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetTierListHandler : IRequestHandler<GetTierListQuery, IEnumerable<SongTierListEntry>>
    {
        private readonly ITierListRepository _tierLists;

        public GetTierListHandler(ITierListRepository tierLists)
        {
            _tierLists = tierLists;
        }

        public async Task<IEnumerable<SongTierListEntry>> Handle(GetTierListQuery request,
            CancellationToken cancellationToken)
        {
            return await _tierLists.GetAllEntries(request.TierListName, cancellationToken);
        }

        public static double StdDev(IEnumerable<int> values,
            bool as_sample)
        {
            // Get the mean.
            double mean = values.Sum() / values.Count();

            // Get the sum of the squares of the differences
            // between the values and the mean.
            var squares_query =
                from int value in values
                select (value - mean) * (value - mean);
            var sum_of_squares = squares_query.Sum();

            if (as_sample)
                return Math.Sqrt(sum_of_squares / (values.Count() - 1));
            return Math.Sqrt(sum_of_squares / values.Count());
        }
    }
}
