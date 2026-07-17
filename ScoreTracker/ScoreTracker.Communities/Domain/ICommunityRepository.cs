using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Domain
{
    internal interface ICommunityRepository
    {
        Task<Name?> GetCommunityByInviteCode(Guid inviteCode, CancellationToken cancellationToken);
        Task SaveCommunity(Community community, CancellationToken cancellationToken);
        Task<IEnumerable<CommunityOverviewRecord>> GetCommunities(Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<CommunityOverviewRecord>> GetPublicCommunities(CancellationToken cancellationToken);

        Task<IEnumerable<CommunityLeaderboardRecord>> GetLeaderboard(MixEnum mix, Name communityName,
            CancellationToken cancellationToken);

        Task<Community?> GetCommunityByName(Name communityName, CancellationToken cancellationToken);

        /// <summary>The names of every community this Discord channel is registered to (may be empty).</summary>
        Task<IReadOnlyList<Name>> GetChannelCommunityNames(ulong channelId, CancellationToken cancellationToken);

        /// <summary>
        ///     Player-made community count: regional (country) communities excluded, all
        ///     privacy types included. Front-door stat — the implementation caches.
        /// </summary>
        Task<int> CountNonRegionalCommunities(CancellationToken cancellationToken);
    }
}
