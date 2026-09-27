using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Who a tool can reach. A tool takes players beyond its maker only while the maker has a Discord
///     account linked, and every read of a tool's players comes through here so that rule is applied
///     in one place: Connect, the directory, a player's connections, the API's access checks, the
///     webhook fan-out, the invite preview and the player counts.
///     <para>
///         The link is read through <see cref="IUserReader" /> — accounts and their sign-ins belong
///         to Identity — and the repository is handed the answer rather than asked to find it.
///     </para>
/// </summary>
internal sealed class ToolReach
{
    /// <summary>The sign-in provider whose link makes a maker reachable.</summary>
    public const string DiscordProvider = "Discord";

    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public ToolReach(IToolRepository tools, IUserReader users)
    {
        _tools = tools;
        _users = users;
    }

    /// <summary>The Discord user id each of these makers has linked. Makers with none are absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> DiscordAccounts(IEnumerable<Guid> makerIds,
        CancellationToken cancellationToken)
    {
        var ids = makerIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, string>();

        return await _users.GetExternalLogins(ids, DiscordProvider, cancellationToken);
    }

    public async Task<bool> MakerHasDiscord(Guid makerId, CancellationToken cancellationToken)
    {
        return (await DiscordAccounts(new[] { makerId }, cancellationToken)).ContainsKey(makerId);
    }

    public async Task<bool> CanTakePlayers(Tool tool, CancellationToken cancellationToken)
    {
        return tool.CanTakePlayers(await MakerHasDiscord(tool.OwnerUserId, cancellationToken));
    }

    public async Task<IReadOnlyList<Guid>> ReadablePlayerIds(Guid toolId, CancellationToken cancellationToken)
    {
        var owner = await _tools.GetOwnerId(toolId, cancellationToken);
        if (owner is null) return Array.Empty<Guid>();

        var takesPlayers = Tool.TakesPlayers(toolId, await MakerHasDiscord(owner.Value, cancellationToken));
        return await _tools.GetReadablePlayerIds(toolId, takesPlayers, cancellationToken);
    }

    public async Task<bool> CanRead(Guid toolId, Guid userId, CancellationToken cancellationToken)
    {
        return (await ReadablePlayerIds(toolId, cancellationToken)).Contains(userId);
    }

    /// <summary>
    ///     How many <b>other</b> people this tool can read. The maker is auto-granted a share when
    ///     they create it, so counting themselves would mean a brand-new tool reports one connected
    ///     player and its own maker is blocked from entering session mode by their own consent.
    /// </summary>
    public async Task<int> CountConnectedPlayers(Tool tool, bool takesPlayers, CancellationToken cancellationToken)
    {
        return (await _tools.GetReadablePlayerIds(tool.Id, takesPlayers, cancellationToken))
            .Count(id => id != tool.OwnerUserId);
    }

    public async Task<int> CountConnectedPlayers(Tool tool, CancellationToken cancellationToken)
    {
        return await CountConnectedPlayers(tool, await CanTakePlayers(tool, cancellationToken), cancellationToken);
    }

    public async Task<int> CountConnectedPlayers(Guid toolId, CancellationToken cancellationToken)
    {
        var owner = await _tools.GetOwnerId(toolId, cancellationToken);
        if (owner is null) return 0;

        return (await ReadablePlayerIds(toolId, cancellationToken)).Count(id => id != owner.Value);
    }

    /// <summary>Every tool a player's data is currently reachable by, direct grants and the pool alike.</summary>
    public async Task<IReadOnlyList<Guid>> ToolIdsReading(Guid userId, CancellationToken cancellationToken)
    {
        var reaching = await _tools.GetToolsReaching(userId, cancellationToken);
        var linked = await DiscordAccounts(reaching.PooledTools.Select(t => t.OwnerUserId), cancellationToken);

        return reaching.DirectToolIds
            .Union(reaching.PooledTools
                .Where(t => Tool.TakesPlayers(t.ToolId, linked.ContainsKey(t.OwnerUserId)))
                .Select(t => t.ToolId))
            .Distinct()
            .ToArray();
    }
}
