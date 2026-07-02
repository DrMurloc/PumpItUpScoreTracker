using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetAllPreferenceRatingsQuery(MixEnum Mix) : IQuery<IEnumerable<ChartPreferenceRatingRecord>>
    {
    }
}
