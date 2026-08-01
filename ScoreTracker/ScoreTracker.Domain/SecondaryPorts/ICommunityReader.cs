using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Communities' published read contract (ADR-001 D3 "pull"), added for the season
///     recap's rival pools. Communities references PlayerProgress, so Progression-side
///     consumers reach memberships through this port — never through a contracts
///     reference (that would cycle the assemblies).
/// </summary>
public interface ICommunityReader
{
    /// <summary>The communities a user belongs to; regional flags distinguish the auto-joined system communities (World + one per country).</summary>
    Task<IEnumerable<CommunityOverviewRecord>> GetUserCommunities(Guid userId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Guid>> GetMembers(Name communityName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     The communities this user created, excluding the system ones (World and the
    ///     per-country communities) — nobody can transfer those, so counting them would block
    ///     every account on the site forever.
    ///     Identity asks through this port because it must not reference Communities: Communities
    ///     already references Identity, and the assemblies would cycle.
    /// </summary>
    Task<IEnumerable<OwnedCommunityRecord>> GetOwnedCommunities(Guid userId,
        CancellationToken cancellationToken = default);
}
