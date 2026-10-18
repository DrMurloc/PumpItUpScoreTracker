namespace ScoreTracker.Domain.Records;

/// <summary>
///     A Discord server the bot can see. <see cref="Name" /> is denormalized onto the
///     designation row for the page; this is where it gets refreshed.
/// </summary>
/// <param name="CanManageRoles">
///     Whether the bot itself holds Manage Roles here. False for every server invited before the
///     title-role feature shipped — the old invite URL never asked for it — and the one condition
///     no amount of role-hierarchy fiddling fixes.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record BotGuild(ulong Id, string Name, bool CanManageRoles);

/// <summary>
///     Why the bot cannot hand out a role. A closed vocabulary, so the page renders a localized
///     sentence rather than anything Discord said — raw client errors are maintainer data
///     (CLAUDE.md, no raw exception text outside Pages/Admin).
/// </summary>
public enum BotRoleBlockedReason
{
    /// <summary>Ranked at or above the bot's own highest role. The common one.</summary>
    AboveBot,

    /// <summary>Owned by another integration or a bot; Discord lets nobody assign it.</summary>
    Managed,

    /// <summary>@everyone. Everyone holds it already and it can be neither granted nor removed.</summary>
    Everyone,

    /// <summary>
    ///     The bot has no Manage Roles permission in this server at all, so nothing here can be
    ///     handed out whatever its position. The fix is re-running the invite URL, not moving
    ///     roles around.
    /// </summary>
    BotCannotManageRoles
}

/// <summary>
///     Why Discord turned down a role write that the bot's own view said would work. The bot can
///     see its permissions and the role order, but not everything Discord checks — a timeout on the
///     bot, a server that requires two-factor authentication for moderator actions — so some
///     refusals only show up as the answer to a write. A closed vocabulary for the same reason as
///     <see cref="BotRoleBlockedReason" />: the page renders a sentence, never Discord's own text.
/// </summary>
public enum BotRoleRefusal
{
    /// <summary>
    ///     50001 Missing Access. What a bot without Manage Roles gets back, and what Discord also
    ///     answers when the cause is one the bot cannot see from its side.
    /// </summary>
    NoAccess,

    /// <summary>50013 Missing Permissions.</summary>
    NoPermission,

    /// <summary>60003: the server requires two-factor authentication for moderator actions.</summary>
    TwoFactorRequired,

    /// <summary>The member left between the read and the write.</summary>
    MemberGone,

    /// <summary>The role was deleted between the read and the write.</summary>
    RoleGone,

    /// <summary>Anything else Discord said no to.</summary>
    Other
}

/// <summary>
///     A role in a Discord server, as the role picker and the reconcile both need it.
///     <para>
///         <see cref="BlockedReason" /> is the whole reason this record exists. Discord refuses a
///         role ranked at or above the bot's own highest role, refuses roles owned by another
///         integration, and refuses <c>@everyone</c> — and it refuses them SILENTLY from the
///         player's side: no error reaches them, the role simply never arrives. So assignability
///         is computed once, here, and a role that fails it is skipped rather than written and
///         rejected.
///     </para>
///     <para><see cref="Color" /> is the role's own hex, or null where Discord reports none.</para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotGuildRole(ulong Id, string Name, string? Color,
    BotRoleBlockedReason? BlockedReason)
{
    public bool CanAssign => BlockedReason is null;
}
