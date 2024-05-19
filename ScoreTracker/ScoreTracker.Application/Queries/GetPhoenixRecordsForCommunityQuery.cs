using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetPhoenixRecordsForCommunityQuery
        (Name CommuityName, Guid ChartId) : IRequest<IEnumerable<UserPhoenixScore>>
    {
    }
}
