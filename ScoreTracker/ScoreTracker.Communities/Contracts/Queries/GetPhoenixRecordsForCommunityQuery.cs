using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPhoenixRecordsForCommunityQuery
        (Name CommuityName, Guid ChartId, MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<UserPhoenixScore>>
    {
    }
}
