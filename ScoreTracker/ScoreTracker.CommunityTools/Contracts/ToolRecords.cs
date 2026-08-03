using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Contracts;

/// <summary>A tool as its maker sees it.</summary>
[ExcludeFromCodeCoverage]
public sealed record ToolRecord(
    Guid Id,
    Guid OwnerUserId,
    string OwnerName,
    string Name,
    string? Description,
    string? Url,
    ToolVisibility Visibility,
    bool AcceptsAllToolsShare,
    WebhookMode WebhookMode,
    string? WebhookUrl,
    IReadOnlyList<MixEnum> Mixes,
    int ConnectedPlayers,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    DateTimeOffset? WebhookUrlVerifiedAt,
    string? OutboundHeaderName,
    bool HasOutboundHeaderValue,
    /// <summary>
    ///     Whether a verification secret is registered. Only ever the flag — the secret is stored as
    ///     a hash and the plaintext exists nowhere after the maker saves it.
    /// </summary>
    bool HasVerificationSecret);

/// <summary>A tool as a player browsing the directory sees it — no delivery configuration.</summary>
[ExcludeFromCodeCoverage]
public sealed record PublicToolRecord(
    Guid Id,
    string Name,
    string? Description,
    string? Url,
    string OwnerName,
    bool RequiresPiuGameSession,
    int ConnectedPlayers,
    DateTimeOffset? ApprovedAt);

/// <summary>One of a player's connections, for the "who can read my scores" list.</summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerToolConnectionRecord(
    Guid ToolId,
    string Name,
    string? Description,
    string OwnerName,
    ShareSource Source,
    bool RequiresPiuGameSession,
    DateTimeOffset GrantedAt);

/// <summary>An API key's metadata. Never the key — after minting nobody can produce it.</summary>
[ExcludeFromCodeCoverage]
public sealed record ApiKeyRecord(
    Guid Id,
    string Name,
    string Last4,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked)
{
    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt is not null && ExpiresAt <= now;
    }

    /// <summary>
    ///     Inside the window where the developer page nags. There are no expiry emails, so the page
    ///     is the whole warning — it has to be loud rather than accurate to the day.
    /// </summary>
    public bool IsExpiringSoon(DateTimeOffset now)
    {
        return ExpiresAt is not null && !IsRevoked && ExpiresAt > now && ExpiresAt <= now.AddDays(14);
    }
}

/// <summary>The one time a key is readable.</summary>
[ExcludeFromCodeCoverage]
public sealed record MintedApiKey(Guid Id, string Key, DateTimeOffset? ExpiresAt);

/// <summary>
///     A live invite link and the maker's private note about it. The note is maker-only — the player
///     who follows the link is never shown it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ToolInviteLinkRecord(Guid Code, string? Note, DateTimeOffset CreatedAt);

/// <summary>What a player is told before connecting through an invite link.</summary>
[ExcludeFromCodeCoverage]
public sealed record ToolInvitePreview(
    Guid ToolId,
    string Name,
    string? Description,
    string? Url,
    string OwnerName,
    bool IsPublic,
    bool RequiresPiuGameSession,
    int ConnectedPlayers);
