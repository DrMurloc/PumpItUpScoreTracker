namespace ScoreTracker.CommunityTools.Domain;

/// <summary>Persistence for API keys and invite codes.</summary>
internal interface IToolKeyRepository
{
    Task<IReadOnlyList<ToolApiKeyRecord>> GetKeys(Guid toolId, CancellationToken cancellationToken = default);

    Task AddKey(Guid toolId, Guid keyId, string name, string hash, string last4, DateTimeOffset createdAt,
        DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);

    Task RevokeKey(Guid toolId, Guid keyId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves a presented key to its tool. A live key stamps last-used, the only signal a
    ///     maker has that a key is still carrying traffic. An expired key comes back named and
    ///     marked, so the console can say which key stopped working; unknown and revoked keys are
    ///     null, and nobody hears about them.
    /// </summary>
    Task<ToolKeyResolution?> ResolveToolByKeyHash(string hash, DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolInviteCodeRecord>> GetInviteCodes(Guid toolId,
        CancellationToken cancellationToken = default);

    Task AddInviteCode(Guid toolId, Guid code, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task RevokeInviteCode(Guid toolId, Guid code, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the maker's private note on one link. Blank clears it.</summary>
    Task SetInviteCodeNote(Guid toolId, Guid code, string? note,
        CancellationToken cancellationToken = default);

    /// <summary>The tool an unrevoked invite code belongs to, or null.</summary>
    Task<Guid?> ResolveToolByInviteCode(Guid code, CancellationToken cancellationToken = default);
}

/// <summary>
///     What a presented key turned out to be. <see cref="IsExpired" /> is the one failure a maker
///     is told about by name: their key, their expiry date, their problem to fix.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolKeyResolution(Guid ToolId, string KeyName, bool IsExpired);

/// <summary>A live invite code and the maker's private note about where they shared it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolInviteCodeRecord(Guid Code, string? Note, DateTimeOffset CreatedAt);

/// <summary>
///     An API key as its maker sees it. Deliberately never carries the key itself — after minting,
///     nobody can produce it, which is the point of hashing at rest.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolApiKeyRecord(
    Guid Id,
    string Name,
    string Last4,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
