using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetUserPreferenceRatingsQuery(MixEnum Mix) : IQuery<IEnumerable<UserRatingsRecord>>
    {
    }
}
