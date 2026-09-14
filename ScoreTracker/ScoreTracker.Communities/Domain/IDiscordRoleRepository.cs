using ScoreTracker.Communities.Contracts;

namespace ScoreTracker.Communities.Domain;

/// <summary>
///     Storage for the Discord role feature: a community's designated server, its title→role
///     table, and the record of who we have granted for.
/// </summary>
internal interface IDiscordRoleRepository
{
    // ---- the designated server ---------------------------------------------------------------

    Task<CommunityDiscordServerRecord?> GetServer(Guid communityId, CancellationToken cancellationToken);

    /// <summary>Every community that has designated a server — the sweep's work list.</summary>
    Task<IReadOnlyList<CommunityDiscordServerRecord>> GetAllServers(CancellationToken cancellationToken);

    /// <summary>
    ///     Communities that hand out roles in this server. Normally one; nothing forbids two
    ///     communities designating the same Discord, and each manages only its own mapped roles.
    /// </summary>
    Task<IReadOnlyList<CommunityDiscordServerRecord>> GetServersByGuild(ulong guildId,
        CancellationToken cancellationToken);

    /// <summary>Designates, or repoints an existing designation at a different server.</summary>
    Task SaveServer(Guid communityId, ulong guildId, string guildName, DateTimeOffset designatedAt,
        CancellationToken cancellationToken);

    Task DeleteServer(Guid communityId, CancellationToken cancellationToken);

    // ---- the title -> role table -------------------------------------------------------------

    Task<IReadOnlyList<CommunityTitleRoleRecord>> GetMappings(Guid communityId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Adds or repoints a title's role. Fails the caller's own validation, not a constraint
    ///     violation, when the role is already spoken for by another title.
    /// </summary>
    Task SaveMapping(Guid communityId, string titleName, ulong roleId, CancellationToken cancellationToken);

    Task DeleteMapping(Guid communityId, string titleName, CancellationToken cancellationToken);

    // ---- grants --------------------------------------------------------------------------------

    Task<IReadOnlyList<CommunityDiscordGrantRecord>> GetGrants(Guid communityId,
        CancellationToken cancellationToken);

    /// <summary>Every grant for one account, across communities. The purge path's read.</summary>
    Task<IReadOnlyList<CommunityDiscordGrantRecord>> GetGrantsForUser(Guid userId,
        CancellationToken cancellationToken);

    Task SaveGrant(Guid communityId, Guid userId, ulong discordUserId, DateTimeOffset reconciledAt,
        CancellationToken cancellationToken);

    Task DeleteGrant(Guid communityId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Drops every grant a community holds — it stopped handing out roles entirely.</summary>
    Task DeleteGrantsForCommunity(Guid communityId, CancellationToken cancellationToken);

    // ---- opt-outs ------------------------------------------------------------------------------

    /// <summary>
    ///     Who has turned this community's title roles off for themselves — the rule's fifth fact
    ///     (docs/design/discord-role-management.md D22), read once per reconcile pass.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetOptedOutUsers(Guid communityId, CancellationToken cancellationToken);

    /// <summary>Records the opt-out. Running it twice keeps the first date.</summary>
    Task SaveOptOut(Guid communityId, Guid userId, DateTimeOffset optedOutAt,
        CancellationToken cancellationToken);

    /// <summary>Lifts it. False when there was nothing to lift, so the command can say so.</summary>
    Task<bool> DeleteOptOut(Guid communityId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Server, mappings, grants and opt-outs together, for a community that no longer exists.
    ///     The community delete does not cascade here, so without this the rows outlive the club.
    /// </summary>
    Task DeleteAllForCommunity(Guid communityId, CancellationToken cancellationToken);
}
