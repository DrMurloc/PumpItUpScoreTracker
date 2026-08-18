using ScoreTracker.Communities.Contracts;
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

        /// <summary>
        ///     Persist the whole community: settings, the full member projection, invite codes and
        ///     channels. Rows missing from the projection are deleted, so callers must hold a
        ///     current aggregate — use <see cref="AddMembership" />/<see cref="RemoveMembership" />
        ///     for a plain join or leave, which touch one row and cannot clobber a concurrent one.
        /// </summary>
        Task SaveCommunity(Community community, CancellationToken cancellationToken);

        /// <summary>
        ///     Add one plain membership row, leaving every other member untouched. A no-op when the
        ///     user already holds a row of any kind (including a retained ban), so joining twice —
        ///     or concurrently — is safe. False means no row was written.
        /// </summary>
        Task<bool> AddMembership(Name communityName, Guid userId, CancellationToken cancellationToken);

        /// <summary>
        ///     Delete one user's membership row. Retained bans and the creator seat stay, matching
        ///     what saving the aggregate does with them.
        /// </summary>
        Task RemoveMembership(Name communityName, Guid userId, CancellationToken cancellationToken);

        /// <summary>
        ///     Delete a community and all of its member/invite/channel/highlight rows. Returns the
        ///     deleted club's id (null when nothing matched): the domain model is name-shaped, and
        ///     the id is what CommunityDeletedEvent carries so other verticals can settle what THEY
        ///     hold against the club.
        /// </summary>
        Task<Guid?> DeleteCommunity(Name communityName, CancellationToken cancellationToken);

        /// <summary>The member roster (including retained bans) joined to user display identity + role.</summary>
        Task<IEnumerable<CommunityMemberRecord>> GetRoster(Name communityName, CancellationToken cancellationToken);

        /// <summary>One row per community the user holds a membership row in (bans included).</summary>
        Task<IEnumerable<MyCommunityRoleRecord>> GetUserRoles(Guid userId, CancellationToken cancellationToken);

        /// <summary>Every membership row of one community as (user, role) pairs, bans included.</summary>
        Task<IEnumerable<CommunityMemberRoleRecord>> GetMemberRoles(Guid communityId,
            CancellationToken cancellationToken);

        /// <summary>Names for the given community ids; unknown ids are absent from the result.</summary>
        Task<IReadOnlyDictionary<Guid, Name>> GetCommunityNames(IReadOnlyCollection<Guid> communityIds,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Directory metadata: every community's Singles/Doubles competitive-level spread over
        ///     members with mix stats and level ≥ 5. Day-cached — the numbers may go stale.
        /// </summary>
        Task<IEnumerable<CommunityCompetitiveRangeRecord>> GetCompetitiveRanges(MixEnum mix,
            CancellationToken cancellationToken);

        Task<IEnumerable<CommunityOverviewRecord>> GetCommunities(Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<CommunityOverviewRecord>> GetPublicCommunities(CancellationToken cancellationToken);

        Task<IEnumerable<CommunityLeaderboardRecord>> GetLeaderboard(MixEnum mix, Name communityName,
            CancellationToken cancellationToken);

        Task<Community?> GetCommunityByName(Name communityName, CancellationToken cancellationToken);

        /// <summary>Every community this Discord channel is registered to, with its regional flag (may be empty).</summary>
        Task<IReadOnlyList<ChannelCommunityInfo>> GetChannelCommunities(ulong channelId,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Player-made community count: regional (country) communities excluded, all
        ///     privacy types included. Front-door stat — the implementation caches.
        /// </summary>
        Task<int> CountNonRegionalCommunities(CancellationToken cancellationToken);
    }
}
