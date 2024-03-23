using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetCommunityLeaderboardQuery
        (Name Community) : IRequest<IEnumerable<CommunityLeaderboardRecord>>
    {
    }
}
