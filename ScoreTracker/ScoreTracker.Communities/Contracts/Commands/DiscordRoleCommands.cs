using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Maps a Phoenix 2 title to a role, or repoints one that already exists.</summary>
[ExcludeFromCodeCoverage]
public sealed record SetCommunityTitleRoleCommand(Name CommunityName, string TitleName, ulong RoleId)
    : IRequest;

/// <summary>
///     Drops a mapping. <paramref name="TakeRoleBack" /> also strips the role from everyone
///     holding it — off by default, because removing a row is a configuration edit and a mass
///     revocation is not (docs/design/discord-role-management.md D13).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveCommunityTitleRoleCommand(Name CommunityName, string TitleName,
    bool TakeRoleBack = false) : IRequest;

/// <summary>
///     Stops handing out roles for this community. Every role it granted comes off first — a role
///     must not outlive the reason it was given (D14).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UnlinkCommunityDiscordServerCommand(Name CommunityName) : IRequest;

/// <summary>The page's Check now button. Runs the same pass the hourly sweep runs, for one community.</summary>
[ExcludeFromCodeCoverage]
public sealed record ReconcileCommunityDiscordRolesCommand(Name CommunityName) : IRequest<int>;
