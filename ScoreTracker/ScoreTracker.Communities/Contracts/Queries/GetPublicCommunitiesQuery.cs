using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed class GetPublicCommunitiesQuery : IQuery<IEnumerable<CommunityOverviewRecord>>
    {
    }
}
