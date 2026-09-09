using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts;

/// <summary>The Discord server a community hands out roles in.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityDiscordServerRecord(Guid CommunityId, ulong GuildId, string GuildName,
    DateTimeOffset DesignatedAt);

/// <summary>One title mapped to one Discord role.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityTitleRoleRecord(string TitleName, ulong RoleId);

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
