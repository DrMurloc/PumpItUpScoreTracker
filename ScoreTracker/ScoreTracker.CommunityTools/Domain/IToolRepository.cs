using ScoreTracker.CommunityTools.Contracts;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>Persistence for tools, their shares, and their keys.</summary>
internal interface IToolRepository
{
    Task<Tool?> GetTool(Guid toolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tool>> GetToolsOwnedBy(Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>Every registered tool. Admin surfaces only — nothing player-facing reads this.</summary>
    Task<IReadOnlyList<Tool>> GetAllTools(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tool>> GetToolsByVisibility(ToolVisibility visibility,
        CancellationToken cancellationToken = default);

    Task Save(Tool tool, CancellationToken cancellationToken = default);

    Task DeleteTool(Guid toolId, CancellationToken cancellationToken = default);

    /// <summary>How many players can currently read this tool's data — the session-mode gate reads it.</summary>
    Task<int> CountConnectedPlayers(Guid toolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolShareRecord>> GetSharesForTool(Guid toolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolShareRecord>> GetSharesForUser(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds or revives a direct grant. Idempotent — connecting twice is not an error.</summary>
    Task GrantShare(Guid toolId, Guid userId, ShareSource source, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    Task RevokeShare(Guid toolId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task BlockTool(Guid toolId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task UnblockTool(Guid toolId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> GetShareWithAllTools(Guid userId, CancellationToken cancellationToken = default);

    Task SetShareWithAllTools(Guid userId, bool share, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>Everything a player's data is currently reachable by, direct grants and the pool alike.</summary>
    Task<IReadOnlyList<Guid>> GetToolIdsReading(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Every player one tool may read, resolved across direct grants, the pool, and blocks.</summary>
    Task<IReadOnlyList<Guid>> GetReadablePlayerIds(Guid toolId, CancellationToken cancellationToken = default);

    Task<bool> CanRead(Guid toolId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     How many live keys each of these tools holds. Batched because the console and the
    ///     directory both need it for a page of rows, and a query per row is a query per row.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountKeysFor(IReadOnlyCollection<Guid> toolIds,
        DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

/// <summary>A player's grant to a tool, as a reader sees it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolShareRecord(Guid ToolId, Guid UserId, ShareSource Source, DateTimeOffset GrantedAt);
