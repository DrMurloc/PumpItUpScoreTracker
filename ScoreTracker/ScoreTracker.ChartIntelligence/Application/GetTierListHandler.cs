using MediatR;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application
{
    internal sealed class GetTierListHandler : IRequestHandler<GetTierListQuery, IEnumerable<SongTierListEntry>>,
        IRequestHandler<GetTierListWithFallbackQuery, TierListResult>
    {
        private readonly ITierListRepository _tierLists;

        public GetTierListHandler(ITierListRepository tierLists)
        {
            _tierLists = tierLists;
        }

        public async Task<IEnumerable<SongTierListEntry>> Handle(GetTierListQuery request,
            CancellationToken cancellationToken)
        {
            return (await GetWithFallback(request.Mix, request.TierListName, cancellationToken)).Entries;
        }

        public async Task<TierListResult> Handle(GetTierListWithFallbackQuery request,
            CancellationToken cancellationToken)
        {
            return await GetWithFallback(request.Mix, request.TierListName, cancellationToken);
        }

        private async Task<TierListResult> GetWithFallback(MixEnum mix, Name tierListName,
            CancellationToken cancellationToken)
        {
            var entries = (await _tierLists.GetAllEntries(mix, tierListName, cancellationToken)).ToArray();
            if (mix != MixEnum.Phoenix2 || entries.Any())
                return new TierListResult(entries, false);

            // Locked decision (plan doc): until Phoenix 2 data accumulates, its tier lists
            // fall back to the Phoenix list, surfaced as provisional so the UI can badge it.
            var fallback = (await _tierLists.GetAllEntries(MixEnum.Phoenix, tierListName, cancellationToken))
                .ToArray();
            return new TierListResult(fallback, true);
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
