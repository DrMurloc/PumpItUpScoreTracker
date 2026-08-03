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
    bool HasVerificationSecret,
    string? RepositoryUrl,
    /// <summary>The account the repository sits under. Displayed for a human, never decided on.</summary>
    string? RepositoryOwner,
    DateTimeOffset? RepositoryCheckedAt,
    /// <summary>The maker's own, or an admin's view of it. Never reaches a player-facing surface.</summary>
    string? DiscordHandle,
    DateTimeOffset? AgreedToRulesAt,
    /// <summary>
    ///     Whether this tool may reach anyone but its maker. Mirrors the domain rule so the console
    ///     can say why a tool is stuck without guessing at it.
    /// </summary>
    bool CanBeSharedWithOthers);

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
    DateTimeOffset? ApprovedAt,
    /// <summary>
    ///     Where to read the source. Null only for a grandfathered tool, and the row simply carries
    ///     no Source link — there is no claim made about every listed tool that one absence breaks.
    /// </summary>
    string? RepositoryUrl);

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
///     Generates the secret a maker's endpoint answers a verification request with.
///     <para>
///         Public because the console offers to generate one and the rest of the vertical is
///         internal. Left as the vertical's own function rather than a page's: a secret assembled
///         at the call site is a secret whose strength nobody reasoned about, and this one is the
///         difference between an endpoint proving itself and merely answering.
///     </para>
/// </summary>
public static class WebhookVerificationSecret
{
    public const string Prefix = "vfy_";

    public static string New()
    {
        return Prefix + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator
            .GetBytes(24)).ToLowerInvariant();
    }
}

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
    int ConnectedPlayers,
    /// <summary>
    ///     Where to read the source. The invite landing page is the one logged-out screen in the
    ///     feature, so it is also the one place a stranger can check the tool before signing in.
    /// </summary>
    string? RepositoryUrl);

/// <summary>
///     A maker's ban, as the admin list shows it. Notes are the owner's own and reach no other
///     surface.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ToolMakerBanRecord(Guid UserId, string UserName, DateTimeOffset BannedAt,
    string? Notes);
