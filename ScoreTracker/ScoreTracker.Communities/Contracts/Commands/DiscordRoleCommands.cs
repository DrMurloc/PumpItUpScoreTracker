using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>
///     Maps a Phoenix 2 title to a role, or repoints one that already exists, and hands the role
///     out straight away — an admin who has just said "this title gets this role" should not have
///     to press anything else for it to happen.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetCommunityTitleRoleCommand(Name CommunityName, string TitleName, ulong RoleId,
    IProgress<DiscordRoleProgress>? Progress = null) : IRequest;

/// <summary>
///     Drops a mapping. <paramref name="TakeRoleBack" /> also strips the role from everyone
///     holding it — ON by default: a role the site handed out and then stopped maintaining is an
///     orphan nobody can clear except by hand, which surprises people more than losing it does
///     (docs/design/discord-role-management.md D13). Untick to keep it as a manual role.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveCommunityTitleRoleCommand(Name CommunityName, string TitleName,
    bool TakeRoleBack = true, IProgress<DiscordRoleProgress>? Progress = null) : IRequest;

/// <summary>
///     Stops handing out roles for this community. Every role it granted comes off first — a role
///     must not outlive the reason it was given (D14).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UnlinkCommunityDiscordServerCommand(Name CommunityName,
    IProgress<DiscordRoleProgress>? Progress = null) : IRequest;

/// <summary>
///     The page's Check now button — the same pass the nightly sweep runs, for one community, on
///     demand. Returns how many members actually changed.
///     <para>
///         <paramref name="Progress" /> is reported as it walks, so the page can draw a real bar
///         instead of a spinner: a big server is a minute of watching nothing otherwise. A MediatR
///         request, never a bus message, so carrying a callback here is in-process and safe.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReconcileCommunityDiscordRolesCommand(Name CommunityName,
    IProgress<DiscordRoleProgress>? Progress = null) : IRequest<int>;
