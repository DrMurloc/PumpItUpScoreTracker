using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed class GetPublicCommunitiesQuery : IRequest<IEnumerable<CommunityOverviewRecord>>
    {
    }
}
