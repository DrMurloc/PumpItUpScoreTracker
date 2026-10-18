using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     The Discord server a community hands out roles in. <see cref="CommunityName" /> rides
///     along so a command run in the server can name the community it acted for without a second
///     read per community.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityDiscordServerRecord(Guid CommunityId, ulong GuildId, string GuildName,
    DateTimeOffset DesignatedAt, string CommunityName = "");

/// <summary>One title mapped to one Discord role.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityTitleRoleRecord(string TitleName, ulong RoleId);

/// <summary>
///     How far a reconcile has got. <see cref="Total" /> is known before the first write, so the
///     bar is deterministic from the outset rather than filling in as it goes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DiscordRoleProgress(int Done, int Total)
{
    public int Percent => Total == 0 ? 100 : (int)(100L * Done / Total);
}

/// <summary>
///     What one pass over a community did. <see cref="Failed" /> is counted apart from
///     <see cref="Changed" /> because the two used to share one number: a pass Discord refused for
///     every member changed nobody, and the page read that zero as "everyone already has the right
///     roles" (docs/design/discord-role-management.md D27).
/// </summary>
/// <param name="Refusal">
///     Set when Discord refused something the whole server shares and the pass stopped there (D28).
///     Null when every failure was about one member.
/// </param>
/// <param name="DiscordCode">Discord's own number for that refusal, printed beside the sentence.</param>
[ExcludeFromCodeCoverage]
public sealed record DiscordRolePassResult(int Changed, int Failed, BotRoleRefusal? Refusal = null,
    int? DiscordCode = null)
{
    public static readonly DiscordRolePassResult Nothing = new(0, 0);
}

/// <summary>A member we have granted roles for, and the Discord account we granted against.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityDiscordGrantRecord(Guid CommunityId, Guid UserId, ulong DiscordUserId,
    DateTimeOffset LastReconciledAt);

/// <summary>
///     A mapping row as the page draws it: the title, the role it points at, how many members
///     hold it today, and — when the bot cannot hand it out — why not.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityTitleRoleView(string TitleName, string? ExclusivityGroup, ulong RoleId,
    string? RoleName, string? RoleColor, BotRoleBlockedReason? BlockedReason, int HolderCount)
{
    /// <summary>True when the role no longer exists in the server at all.</summary>
    public bool RoleMissing => RoleName == null;
}

/// <summary>
///     What the next reconcile would do to one member — role names, not ids, because the page is
///     the only consumer. Produced by the same planner the real pass uses.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DiscordRolePlanRecord(Guid UserId, IReadOnlyList<string> Granting,
    IReadOnlyList<string> Revoking, string? BlockedTitle);

/// <summary>
///     The page's state. <see cref="ViewerCanManage" /> gates every control; the read itself is
///     open, because the roster already is.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityDiscordView(
    Guid CommunityId,
    Name CommunityName,
    CommunityDiscordServerRecord? Server,
    bool BotIsInServer,
    bool BotCanManageRoles,
    bool ViewerCanManage,
    bool ViewerHasDiscordLinked,
    IReadOnlyList<CommunityTitleRoleView> Mappings,
    int MemberCount,
    int MembersHoldingRoles,
    DateTimeOffset? LastReconciledAt)
{
    /// <summary>
    ///     Mapped roles the bot cannot hand out. The failure Discord reports to nobody, so the
    ///     page has to.
    /// </summary>
    public IReadOnlyList<CommunityTitleRoleView> Blocked =>
        Mappings.Where(m => m.BlockedReason != null || m.RoleMissing).ToArray();
}

/// <summary>One member's pending change, as the dry-run table draws it.</summary>
[ExcludeFromCodeCoverage]
public sealed record DiscordRoleChangeRecord(Guid UserId, Name PlayerName, Uri ProfileImage,
    IReadOnlyList<string> Granting, IReadOnlyList<string> Revoking, string? Note);
