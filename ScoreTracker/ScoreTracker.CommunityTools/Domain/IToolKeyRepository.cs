namespace ScoreTracker.CommunityTools.Domain;

/// <summary>Persistence for API keys and invite codes.</summary>
internal interface IToolKeyRepository
{
    Task<IReadOnlyList<ToolApiKeyRecord>> GetKeys(Guid toolId, CancellationToken cancellationToken = default);

    Task AddKey(Guid toolId, Guid keyId, string name, string hash, string last4, DateTimeOffset createdAt,
        DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);

    Task RevokeKey(Guid toolId, Guid keyId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves a presented key to its tool, or null when it is unknown, revoked or expired.
    ///     Also stamps last-used, which is the only signal a maker has that a key is still live.
    /// </summary>
    Task<Guid?> ResolveToolByKeyHash(string hash, DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetInviteCodes(Guid toolId, CancellationToken cancellationToken = default);

    Task AddInviteCode(Guid toolId, Guid code, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task RevokeInviteCode(Guid toolId, Guid code, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>The tool an unrevoked invite code belongs to, or null.</summary>
    Task<Guid?> ResolveToolByInviteCode(Guid code, CancellationToken cancellationToken = default);
}

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
