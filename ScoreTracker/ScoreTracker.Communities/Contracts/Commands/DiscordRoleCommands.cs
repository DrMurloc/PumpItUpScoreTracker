using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Maps a Phoenix 2 title to a role, or repoints one that already exists.</summary>
[ExcludeFromCodeCoverage]
public sealed record SetCommunityTitleRoleCommand(Name CommunityName, string TitleName, ulong RoleId)
    : IRequest;

/// <summary>
///     Drops a mapping. <paramref name="TakeRoleBack" /> also strips the role from everyone
///     holding it — ON by default: a role the site handed out and then stopped maintaining is an
///     orphan nobody can clear except by hand, which surprises people more than losing it does
///     (docs/design/discord-role-management.md D13). Untick to keep it as a manual role.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveCommunityTitleRoleCommand(Name CommunityName, string TitleName,
    bool TakeRoleBack = true) : IRequest;

/// <summary>
///     Stops handing out roles for this community. Every role it granted comes off first — a role
///     must not outlive the reason it was given (D14).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UnlinkCommunityDiscordServerCommand(Name CommunityName) : IRequest;

/// <summary>The page's Check now button. Runs the same pass the hourly sweep runs, for one community.</summary>
[ExcludeFromCodeCoverage]
public sealed record ReconcileCommunityDiscordRolesCommand(Name CommunityName) : IRequest<int>;
