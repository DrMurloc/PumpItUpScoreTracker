using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed class GetPublicCommunitiesQuery : IQuery<IEnumerable<CommunityOverviewRecord>>
    {
    }
}
