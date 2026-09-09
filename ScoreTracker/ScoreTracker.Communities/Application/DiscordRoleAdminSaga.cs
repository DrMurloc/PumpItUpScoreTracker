using MassTransit;
using MediatR;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     What /Community/Discord reads and writes. The rule itself lives in
///     <see cref="DiscordRoleSaga" />; this is the admin surface over its configuration, and it
///     never decides who should hold what.
/// </summary>
internal sealed class DiscordRoleAdminSaga :
    IRequestHandler<GetCommunityDiscordQuery, CommunityDiscordView>,
    IRequestHandler<GetCommunityDiscordRolesQuery, IReadOnlyList<BotGuildRole>>,
    IRequestHandler<GetCommunityDiscordPreviewQuery, IReadOnlyList<DiscordRoleChangeRecord>>,
    IRequestHandler<SetCommunityTitleRoleCommand>,
    IRequestHandler<RemoveCommunityTitleRoleCommand>,
    IRequestHandler<UnlinkCommunityDiscordServerCommand>,
    IRequestHandler<ReconcileCommunityDiscordRolesCommand, int>
{
    /// <summary>The shipped Phoenix 2 titles by name — the mapping picker's whole vocabulary.</summary>
    private static readonly IReadOnlyDictionary<string, Title> TitlesByName =
        Phoenix2TitleList.BuildList().ToDictionary(t => (string)t.Name, t => (Title)t, StringComparer.Ordinal);

    private readonly IBotClient _bot;
    private readonly IBus _bus;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDiscordRoleService _discordRoles;
    private readonly IDiscordRoleRepository _roles;
    private readonly ITitleRepository _titles;
    private readonly IUserReader _users;

    public DiscordRoleAdminSaga(IDiscordRoleRepository roles, ICommunityRepository communities,
        IBotClient bot, ICurrentUserAccessor currentUser, IDiscordRoleService discordRoles,
        ITitleRepository titles, IUserReader users, IBus bus)
    {
        _roles = roles;
        _communities = communities;
        _bot = bot;
        _currentUser = currentUser;
        _discordRoles = discordRoles;
        _titles = titles;
        _users = users;
        _bus = bus;
    }

    public async Task<CommunityDiscordView> Handle(GetCommunityDiscordQuery request,
        CancellationToken cancellationToken)
    {
        var (community, communityId) = await Load(request.CommunityName, cancellationToken);
        var viewerId = _currentUser.IsLoggedIn ? _currentUser.User.Id : Guid.Empty;
        var server = await _roles.GetServer(communityId, cancellationToken);

        var roles = server == null
            ? Array.Empty<BotGuildRole>()
            : (await _bot.GetGuildRoles(server.GuildId, cancellationToken)).ToArray();
        var byId = roles.ToDictionary(r => r.Id);

        var mappings = await _roles.GetMappings(communityId, cancellationToken);
        var grants = await _roles.GetGrants(communityId, cancellationToken);
        var holders = await HolderCounts(community, mappings, cancellationToken);

        var views = mappings.Select(m =>
        {
            var role = byId.GetValueOrDefault(m.RoleId);
            return new CommunityTitleRoleView(m.TitleName, GroupNameOf(m.TitleName), m.RoleId,
                role?.Name, role?.Color, role?.BlockedReason, holders.GetValueOrDefault(m.TitleName));
        }).ToArray();

        var canManage = viewerId != Guid.Empty &&
                        community.HasPermission(viewerId, CommunityPermission.ManageDiscord);

        // One read answers both: whether the bot is still in the server, and whether it holds the
        // permission at all. The second is false for every server invited before this feature.
        var guild = server == null ? null : await _bot.GetGuild(server.GuildId, cancellationToken);

        return new CommunityDiscordView(communityId, community.Name, server,
            guild != null, guild?.CanManageRoles ?? false,
            canManage,
            // Only asked when it can matter: designating a server is done as yourself in Discord,
            // so the page tells an admin up front rather than letting them find out mid-command.
            canManage && await HasDiscordLinked(viewerId, cancellationToken),
            views, community.MemberIds.Count, grants.Count,
            grants.Count == 0 ? null : grants.Max(g => g.LastReconciledAt));
    }

    public async Task<IReadOnlyList<BotGuildRole>> Handle(GetCommunityDiscordRolesQuery request,
        CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);

        var server = await _roles.GetServer(communityId, cancellationToken);
        return server == null
            ? Array.Empty<BotGuildRole>()
            : await _bot.GetGuildRoles(server.GuildId, cancellationToken);
    }

    public async Task<IReadOnlyList<DiscordRoleChangeRecord>> Handle(
        GetCommunityDiscordPreviewQuery request, CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);

        var pending = await _discordRoles.PreviewCommunity(communityId, cancellationToken);
        if (pending.Count == 0) return Array.Empty<DiscordRoleChangeRecord>();

        var users = (await _users.GetUsers(pending.Select(p => p.UserId), cancellationToken))
            .ToDictionary(u => u.Id);

        return pending
            .Where(p => users.ContainsKey(p.UserId))
            .Select(p => new DiscordRoleChangeRecord(p.UserId, users[p.UserId].Name,
                users[p.UserId].ProfileImage, p.Granting, p.Revoking, p.BlockedTitle))
            .ToArray();
    }

    public async Task Handle(SetCommunityTitleRoleCommand request, CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);

        if (!TitlesByName.ContainsKey(request.TitleName))
            throw new CommunityPermissionException("That isn't a Phoenix 2 title.");

        // Two titles pointing at one role would leave "should this role come off" with no single
        // answer, so claiming a role takes it from whatever held it.
        var existing = await _roles.GetMappings(communityId, cancellationToken);
        foreach (var clash in existing.Where(m => m.RoleId == request.RoleId && m.TitleName != request.TitleName))
            await _roles.DeleteMapping(communityId, clash.TitleName, cancellationToken);

        await _roles.SaveMapping(communityId, request.TitleName, request.RoleId, cancellationToken);

        // Inline, not published: saying "this title gets this role" and then being told to press
        // Check now reads as though nothing happened. The caller watches it work.
        await _discordRoles.ReconcileCommunity(communityId, cancellationToken, request.Progress);
    }

    public async Task Handle(RemoveCommunityTitleRoleCommand request, CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);

        var mapping = (await _roles.GetMappings(communityId, cancellationToken))
            .FirstOrDefault(m => m.TitleName == request.TitleName);
        if (mapping == null) return;

        // Taking it back has to happen while the mapping still says the role is ours — once the
        // row is gone, nothing knows this community ever managed it.
        if (request.TakeRoleBack)
            await _discordRoles.RevokeRole(communityId, mapping.RoleId, cancellationToken,
                request.Progress);

        await _roles.DeleteMapping(communityId, request.TitleName, cancellationToken);
    }

    public async Task Handle(UnlinkCommunityDiscordServerCommand request, CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);

        // Roles come off before the designation does — afterwards nothing knows which server they
        // were in (D14).
        await _discordRoles.RevokeAll(communityId, cancellationToken, request.Progress);
        await _roles.DeleteServer(communityId, cancellationToken);
    }

    public async Task<int> Handle(ReconcileCommunityDiscordRolesCommand request,
        CancellationToken cancellationToken)
    {
        var (_, communityId) = await Load(request.CommunityName, cancellationToken);
        await EnsureCanManage(request.CommunityName, cancellationToken);
        return await _discordRoles.ReconcileCommunity(communityId, cancellationToken, request.Progress);
    }

    // ---- helpers ----------------------------------------------------------------------------

    private async Task<(Community Community, Guid Id)> Load(Name communityName,
        CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(communityName, cancellationToken)
                        ?? throw new CommunityNotFoundException();
        var id = await _communities.GetCommunityId(communityName, cancellationToken)
                 ?? throw new CommunityNotFoundException();
        return (community, id);
    }

    private async Task EnsureCanManage(Name communityName, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(communityName, cancellationToken)
                        ?? throw new CommunityNotFoundException();
        if (!_currentUser.IsLoggedIn ||
            !community.HasPermission(_currentUser.User.Id, CommunityPermission.ManageDiscord))
            throw new CommunityPermissionException("You cannot manage Discord for this community.");
    }

    private async Task<bool> HasDiscordLinked(Guid userId, CancellationToken cancellationToken) =>
        (await _users.GetExternalLogins(new[] { userId }, DiscordRoleSaga.DiscordProvider,
            cancellationToken)).ContainsKey(userId);

    private static string? GroupNameOf(string titleName) =>
        TitlesByName.TryGetValue(titleName, out var title)
            ? TitleExclusivity.GroupOf(title)?.ToString()
            : null;

    /// <summary>
    ///     How many members hold each mapped title today. One read for every holder of every mapped
    ///     title, intersected with the roster — a per-title count would be one query per row.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>> HolderCounts(Community community,
        IReadOnlyList<CommunityTitleRoleRecord> mappings, CancellationToken cancellationToken)
    {
        if (mappings.Count == 0) return new Dictionary<string, int>();

        var titles = mappings.Select(m => m.TitleName)
            .Where(TitlesByName.ContainsKey)
            .Select(Name.From)
            .ToArray();
        if (titles.Length == 0) return new Dictionary<string, int>();

        var holders = await _titles.GetUsersWithTitles(MixEnum.Phoenix2, titles, cancellationToken);
        return holders
            .Where(h => community.MemberIds.Contains(h.UserId))
            .GroupBy(h => (string)h.Title)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }
}
