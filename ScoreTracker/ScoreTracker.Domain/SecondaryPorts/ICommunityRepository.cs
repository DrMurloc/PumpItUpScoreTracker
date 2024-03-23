using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ICommunityRepository
    {
        Task<Name?> GetCommunityByInviteCode(Guid inviteCode, CancellationToken cancellationToken);
        Task SaveCommunity(Community community, CancellationToken cancellationToken);
        Task<IEnumerable<CommunityOverviewRecord>> GetCommunities(Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<CommunityOverviewRecord>> GetPublicCommunities(CancellationToken cancellationToken);

        Task<IEnumerable<CommunityLeaderboardRecord>> GetLeaderboard(Name communityName,
            CancellationToken cancellationToken);

        Task<Community?> GetCommunityByName(Name communityName, CancellationToken cancellationToken);
    }
}
