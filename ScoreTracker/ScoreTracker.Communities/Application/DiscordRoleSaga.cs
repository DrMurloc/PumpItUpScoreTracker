using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Keeps a community's Discord roles in step with its members' Phoenix 2 titles.
///     <para>
///         The whole feature is one rule, and <see cref="ReconcileMember" /> is the only place it
///         is written down: a member holds a mapped role exactly while they are in the community,
///         in its designated server, holding the title, with no higher rung of the same pumbility
///         pool. Every trigger — a title earned, a join, a leave, a ban, a purge, an admin edit,
///         the sweep — funnels here, so nothing can compute roles a second way and disagree.
///     </para>
///     <para>
///         Reconciling is idempotent and reads current truth rather than a delta, which is what
///         makes a dropped in-memory bus message survivable: the next trigger, or the sweep, fixes
///         it.
///     </para>
/// </summary>
internal sealed class DiscordRoleSaga
{
    /// <summary>
    ///     The external-login provider name, matching the one LoginController challenges under.
    ///     A mismatch here does not throw — it silently resolves every member to "no Discord
    ///     linked" and quietly hands out nothing.
    /// </summary>
    internal const string DiscordProvider = "Discord";

    /// <summary>Phoenix 2 only (docs/design/discord-role-management.md D1).</summary>
    private const MixEnum RoleMix = MixEnum.Phoenix2;

    /// <summary>
    ///     The shipped title list by name. A mapping can outlive its title — the game retiring one
    ///     should stop handing out a role, not take a whole reconcile down — so lookups miss
    ///     rather than throw.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Title> TitlesByName =
        Phoenix2TitleList.BuildList().ToDictionary(t => (string)t.Name, t => (Title)t, StringComparer.Ordinal);

    private readonly IBotClient _bot;
    private readonly ICommunityRepository _communities;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<DiscordRoleSaga> _logger;
    private readonly IDiscordRoleRepository _roles;
    private readonly IMediator _mediator;
    private readonly ITitleRepository _titles;
    private readonly IUserReader _users;

    public DiscordRoleSaga(IDiscordRoleRepository roles, ICommunityRepository communities,
        ITitleRepository titles, IUserReader users, IBotClient bot, IMediator mediator,
        IDateTimeOffsetAccessor dateTime, ILogger<DiscordRoleSaga> logger)
    {
        _roles = roles;
        _communities = communities;
        _titles = titles;
        _users = users;
        _bot = bot;
        _mediator = mediator;
        _dateTime = dateTime;
        _logger = logger;
    }

    // ---- entry points -----------------------------------------------------------------------

    /// <summary>
    ///     Settles one member in one community. The entry point every event-shaped trigger uses.
    /// </summary>
    public async Task ReconcileOne(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        var context = await BuildContext(communityId, cancellationToken);
        if (context == null) return;

        await ReconcileMember(context, userId, await ResolveSnowflake(context, userId, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    ///     Settles every member of one community, plus everyone it still holds a grant for. The
    ///     second half is the point: somebody who left, was banned, or unlinked their Discord is no
    ///     longer in the roster, so a roster-only pass would leave their roles standing forever.
    /// </summary>
    public async Task<int> ReconcileCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        var context = await BuildContext(communityId, cancellationToken);
        if (context == null) return 0;

        var granted = (await _roles.GetGrants(communityId, cancellationToken))
            .ToDictionary(g => g.UserId, g => g.DiscordUserId);
        var candidates = context.LinkedMembers.Keys.Concat(context.Members.Keys)
            .Concat(granted.Keys).Distinct().ToArray();

        var changed = 0;
        foreach (var userId in candidates)
        {
            // A member who unlinked has no login left, so the grant row is the only surviving
            // handle on the account we granted against — and the only way to take it back.
            var snowflake = context.LinkedMembers.TryGetValue(userId, out var linked)
                ? linked
                : granted.TryGetValue(userId, out var previous) ? previous : (ulong?)null;
            if (await ReconcileMember(context, userId, snowflake, cancellationToken)) changed++;
        }

        return changed;
    }

    /// <summary>
    ///     Somebody walked into a server. Every community that designated it gets a look — normally
    ///     one; two communities pointing at one Discord is not forbidden, and each manages only its
    ///     own mapped roles.
    /// </summary>
    public async Task ReconcileGuildMember(ulong guildId, ulong discordUserId,
        CancellationToken cancellationToken)
    {
        var servers = await _roles.GetServersByGuild(guildId, cancellationToken);
        if (servers.Count == 0) return;

        // Identity's published contract, not a second read of its own: adding a port member for
        // this would give the same question two answers that could drift.
        var user = await _mediator.Send(
            new GetUserByExternalLoginQuery(discordUserId.ToString(), DiscordProvider), cancellationToken);
        if (user == null) return;

        foreach (var server in servers)
            await ReconcileOne(server.CommunityId, user.Id, cancellationToken);
    }

    /// <summary>
    ///     Strips every role this account holds through any community, then forgets it. Runs on the
    ///     purge path BEFORE the grant rows are deleted — they are the last handle on the Discord
    ///     account once Identity has dropped the external login, and Identity drops it on the very
    ///     first purge pass.
    /// </summary>
    public async Task RemoveAllForUser(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var grant in await _roles.GetGrantsForUser(userId, cancellationToken))
        {
            var context = await BuildContext(grant.CommunityId, cancellationToken);
            if (context != null)
                await Apply(context, grant.DiscordUserId, Array.Empty<ulong>(), cancellationToken);
            await _roles.DeleteGrant(grant.CommunityId, userId, cancellationToken);
        }
    }

    /// <summary>
    ///     Takes back everything a community handed out. Called before a designation is repointed
    ///     or dropped, so roles never outlive the reason for them (D14), and when the community
    ///     itself is deleted.
    /// </summary>
    public async Task RevokeAll(Guid communityId, CancellationToken cancellationToken)
    {
        var context = await BuildContext(communityId, cancellationToken);
        if (context != null)
            foreach (var grant in await _roles.GetGrants(communityId, cancellationToken))
                await Apply(context, grant.DiscordUserId, Array.Empty<ulong>(), cancellationToken);

        await _roles.DeleteGrantsForCommunity(communityId, cancellationToken);
    }

    // ---- the rule ---------------------------------------------------------------------------

    /// <summary>
    ///     The four facts. Returns whether anything actually changed in Discord.
    /// </summary>
    private async Task<bool> ReconcileMember(ReconcileContext context, Guid userId, ulong? discordUserId,
        CancellationToken cancellationToken)
    {
        // No Discord account is linked and none was ever granted against: there is nobody in the
        // server to act on, and nothing to remember.
        if (discordUserId is not { } snowflake)
        {
            await _roles.DeleteGrant(context.CommunityId, userId, cancellationToken);
            return false;
        }

        var eligible = IsMember(context, userId) && context.LinkedMembers.ContainsKey(userId);
        var desired = eligible
            ? await DesiredRoles(context, userId, cancellationToken)
            : Array.Empty<ulong>();

        var changed = await Apply(context, snowflake, desired, cancellationToken);

        if (eligible)
            await _roles.SaveGrant(context.CommunityId, userId, snowflake, _dateTime.Now, cancellationToken);
        else
            await _roles.DeleteGrant(context.CommunityId, userId, cancellationToken);

        return changed;
    }

    /// <summary>
    ///     Which mapped roles this member has earned: the titles they hold that the community maps,
    ///     thinned to one rung per pumbility pool, then to the roles the bot can actually assign.
    /// </summary>
    private async Task<IReadOnlyCollection<ulong>> DesiredRoles(ReconcileContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        var held = (await _titles.GetCompletedTitles(RoleMix, userId, cancellationToken))
            .Select(t => (string)t.Title)
            .ToHashSet(StringComparer.Ordinal);

        var earned = context.Mappings
            .Where(m => held.Contains(m.TitleName) && TitlesByName.ContainsKey(m.TitleName))
            .ToArray();

        return TitleExclusivity.HighestOnly(earned, m => TitlesByName[m.TitleName])
            .Select(m => m.RoleId)
            .Where(context.Assignable.Contains)
            .ToArray();
    }

    /// <summary>
    ///     Writes the difference and nothing else. A role the bot cannot assign is never written:
    ///     Discord would reject it, and the rejection is invisible to the player, so spending the
    ///     call would only turn a visible configuration problem into a silent one.
    /// </summary>
    private async Task<bool> Apply(ReconcileContext context, ulong discordUserId,
        IReadOnlyCollection<ulong> desired, CancellationToken cancellationToken)
    {
        var current = await _bot.GetMemberRoles(context.GuildId, discordUserId, cancellationToken);
        // Not in the server. Discord took the roles with them when they left, so there is nothing
        // to remove and nothing we could grant.
        if (current == null) return false;

        var changed = false;
        foreach (var roleId in desired.Where(r => !current.Contains(r)))
        {
            await _bot.AddRole(context.GuildId, discordUserId, roleId, cancellationToken);
            changed = true;
        }

        // Only ever mapped roles: a role this community does not manage belongs to somebody else.
        foreach (var roleId in context.Managed.Where(r => current.Contains(r) && !desired.Contains(r)))
        {
            await _bot.RemoveRole(context.GuildId, discordUserId, roleId, cancellationToken);
            changed = true;
        }

        return changed;
    }

    // ---- context ----------------------------------------------------------------------------

    /// <summary>
    ///     Everything a reconcile needs that is per-community rather than per-member, read once so
    ///     a sweep over a roster does not re-read it per person. Null when the community hands out
    ///     no roles at all — the common case, and two reads to establish.
    /// </summary>
    private async Task<ReconcileContext?> BuildContext(Guid communityId, CancellationToken cancellationToken)
    {
        var server = await _roles.GetServer(communityId, cancellationToken);
        if (server == null) return null;

        var mappings = await _roles.GetMappings(communityId, cancellationToken);
        if (mappings.Count == 0) return null;

        var roles = await _bot.GetGuildRoles(server.GuildId, cancellationToken);
        // A guild the bot cannot see reports no roles. Nothing is assignable and nothing is
        // managed, so a reconcile becomes a no-op rather than a mass revocation — which is the
        // right way to fail, but worth saying out loud.
        if (roles.Count == 0)
            _logger.LogWarning("Community {CommunityId} designates guild {GuildId}, which the bot cannot see",
                communityId, server.GuildId);

        var assignable = roles.Where(r => r.CanAssign).Select(r => r.Id).ToHashSet();
        var live = roles.Select(r => r.Id).ToHashSet();
        var members = (await _communities.GetMemberRoles(communityId, cancellationToken))
            .ToDictionary(m => m.UserId, m => m.Role);
        var linked = await _users.GetExternalLogins(members.Keys, DiscordProvider, cancellationToken);

        return new ReconcileContext(communityId, server.GuildId, mappings, assignable,
            // Removal is not gated on assignability the way granting is: a role that moved above
            // the bot after we handed it out simply cannot come off, and Discord tells us so, but
            // one that is merely missing from the server must not be asked for at all.
            mappings.Select(m => m.RoleId).Where(live.Contains).ToHashSet(),
            members, Snowflakes(linked));
    }

    /// <summary>
    ///     The member's Discord account for a one-off reconcile: their live link if they are a
    ///     member, otherwise whatever we last granted against — which is all that is left of
    ///     somebody who unlinked or left.
    /// </summary>
    private async Task<ulong?> ResolveSnowflake(ReconcileContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        if (context.LinkedMembers.TryGetValue(userId, out var linked)) return linked;

        var grants = await _roles.GetGrantsForUser(userId, cancellationToken);
        return grants.FirstOrDefault(g => g.CommunityId == context.CommunityId)?.DiscordUserId;
    }

    private static bool IsMember(ReconcileContext context, Guid userId) =>
        context.Members.TryGetValue(userId, out var role) && role != CommunityRole.Banned;

    /// <summary>
    ///     External ids are stored as text; a Discord id that will not parse is not one we can act
    ///     on, so it drops out rather than throwing a sweep over.
    /// </summary>
    private static IReadOnlyDictionary<Guid, ulong> Snowflakes(IReadOnlyDictionary<Guid, string> linked)
    {
        var parsed = new Dictionary<Guid, ulong>();
        foreach (var (userId, raw) in linked)
            if (ulong.TryParse(raw, out var id))
                parsed[userId] = id;
        return parsed;
    }

    private sealed record ReconcileContext(
        Guid CommunityId,
        ulong GuildId,
        IReadOnlyList<CommunityTitleRoleRecord> Mappings,
        IReadOnlySet<ulong> Assignable,
        IReadOnlySet<ulong> Managed,
        IReadOnlyDictionary<Guid, CommunityRole> Members,
        IReadOnlyDictionary<Guid, ulong> LinkedMembers);
}
