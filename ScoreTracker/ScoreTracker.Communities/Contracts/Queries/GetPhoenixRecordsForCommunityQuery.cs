using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPhoenixRecordsForCommunityQuery
        (Name CommuityName, Guid ChartId) : IQuery<IEnumerable<UserPhoenixScore>>
    {
    }
}
