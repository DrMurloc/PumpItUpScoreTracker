namespace ScoreTracker.Communities.Application;

/// <summary>
///     The reconcile, as its collaborators inside this vertical see it. An interface rather than
///     the saga itself because two callers need it in a specific order relative to work they do
///     themselves — the account purge takes roles back before deleting the row that says which
///     Discord account they were on, and the community delete does it before the mappings that
///     say which roles are ours go away — and neither of those orderings can be asserted against a
///     sealed class.
/// </summary>
internal interface IDiscordRoleService
{
    /// <summary>Settles one member in one community.</summary>
    Task ReconcileOne(Guid communityId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Settles a whole community: every member, plus everyone it still holds a grant for.
    ///     Returns how many people actually changed.
    /// </summary>
    Task<int> ReconcileCommunity(Guid communityId, CancellationToken cancellationToken);

    /// <summary>Settles somebody who just walked into a server, in every community that uses it.</summary>
    Task ReconcileGuildMember(ulong guildId, ulong discordUserId, CancellationToken cancellationToken);

    /// <summary>Takes back every role this account holds anywhere, then forgets it.</summary>
    Task RemoveAllForUser(Guid userId, CancellationToken cancellationToken);

    /// <summary>Takes back everything one community handed out.</summary>
    Task RevokeAll(Guid communityId, CancellationToken cancellationToken);
}
