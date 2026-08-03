using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.CommunityTools.Infrastructure.Entities;

/// <summary>A registered community tool.</summary>
internal sealed class ToolEntity
{
    [Key] public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    [Required] [MaxLength(64)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(500)] public string? Url { get; set; }

    /// <summary>Stored as the enum name so a reordered enum cannot silently relabel every row.</summary>
    [Required]
    [MaxLength(20)]
    public string Visibility { get; set; } = string.Empty;

    public bool AcceptsAllToolsShare { get; set; }
    [Required] [MaxLength(20)] public string WebhookMode { get; set; } = string.Empty;
    [MaxLength(500)] public string? WebhookUrl { get; set; }

    /// <summary>A header the maker asks us to send so they can recognise our call.</summary>
    [MaxLength(64)]
    public string? OutboundHeaderName { get; set; }

    /// <summary>
    ///     Encrypted, not hashed. We send it verbatim on every delivery, so it has to be readable
    ///     back — AES-GCM under a data key wrapped by the master key, so the database alone does not
    ///     yield it. Long enough for the base64 envelope rather than for the secret.
    /// </summary>
    [MaxLength(1024)]
    public string? OutboundHeaderValue { get; set; }

    /// <summary>
    ///     SHA-256 of the secret a maker's endpoint answers a verification request with. Hashed
    ///     rather than encrypted because we only ever compare — and it must never travel to the
    ///     endpoint, which is the entire reason it proves anything.
    /// </summary>
    [MaxLength(128)]
    public string? WebhookVerificationSecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    [MaxLength(500)] public string? RejectionReason { get; set; }

    /// <summary>
    ///     When the maker last echoed our challenge back from <see cref="WebhookUrl" />. Null blocks
    ///     every delivery — a configured URL is a claim, this is the proof.
    /// </summary>
    public DateTimeOffset? WebhookUrlVerifiedAt { get; set; }

    /// <summary>
    ///     Where players read this tool's source. Nullable because PIU Tracker predates the
    ///     requirement — the gate is what enforces it, not the column.
    /// </summary>
    [MaxLength(500)]
    public string? RepositoryUrl { get; set; }

    /// <summary>The account the repository sits under, parsed from the URL for the admin list.</summary>
    [MaxLength(100)]
    public string? RepositoryOwner { get; set; }

    /// <summary>When <see cref="RepositoryUrl" /> last answered anonymously.</summary>
    public DateTimeOffset? RepositoryCheckedAt { get; set; }

    /// <summary>The maker's, for the owner to reach them on. Never leaves an admin surface.</summary>
    [MaxLength(64)]
    public string? DiscordHandle { get; set; }

    /// <summary>When the maker accepted the rules, recorded once at registration.</summary>
    public DateTimeOffset? AgreedToRulesAt { get; set; }
}

/// <summary>Which mixes' imports trigger a delivery for this tool.</summary>
internal sealed class ToolMixSubscriptionEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public Guid MixId { get; set; }
}

/// <summary>A private tool's recruiting link, keyed by the code itself as CommunityInviteCode is.</summary>
internal sealed class ToolInviteCodeEntity
{
    [Key] public Guid InviteCode { get; set; }
    public Guid ToolId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    ///     The maker's own reminder of where they put this link. Never shown to the player who
    ///     follows it — a link posted in one discord and a link handed to one friend are the same
    ///     row otherwise, and revoking the right one is guesswork.
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }
}

/// <summary>A player's grant to one tool.</summary>
internal sealed class ToolShareEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Direct or AllTools — what a revoke means differs between them.</summary>
    [Required]
    [MaxLength(20)]
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

/// <summary>
///     An all-tools player's "not this one". Without it the only way to refuse a single tool would
///     be to turn off sharing entirely.
/// </summary>
internal sealed class ToolBlockEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset BlockedAt { get; set; }
}

/// <summary>
///     Whether a player shares with every approved tool.
///     <para>
///         Lives here rather than on the user row: it is authorization data this vertical owns, and
///         keeping it beside shares and blocks makes the effective-access check one local join
///         instead of a read across two verticals' tables.
///     </para>
/// </summary>
internal sealed class ToolSharePreferenceEntity
{
    [Key] public Guid UserId { get; set; }
    public bool ShareWithAllTools { get; set; }
    public DateTimeOffset SetAt { get; set; }
}

/// <summary>A tool's API credential. Hashed at rest; the plaintext is shown once at creation.</summary>
internal sealed class ToolApiKeyEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    [Required] [MaxLength(64)] public string Name { get; set; } = string.Empty;
    [Required] [MaxLength(128)] public string KeyHash { get; set; } = string.Empty;

    /// <summary>The visible tail, so a maker can tell two keys apart without seeing either.</summary>
    [Required]
    [MaxLength(8)]
    public string Last4 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Null means no expiry — allowed, warned about, and rare by design.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

/// <summary>
///     One delivery attempt.
///     <para>
///         <see cref="Body" /> is kept only while it can still be used — a failed or pending delivery
///         that may be retried or replayed, and the most recent success per tool as the signature
///         sample. It is never written for a session-mode delivery: that body carries a live
///         piugame.com credential.
///     </para>
/// </summary>
internal sealed class WebhookDeliveryEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public Guid UserId { get; set; }
    public Guid MixId { get; set; }
    [Required] [MaxLength(20)] public string Mode { get; set; } = string.Empty;
    [Required] [MaxLength(40)] public string DeliveryId { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public int Attempt { get; set; }
    [Required] [MaxLength(20)] public string Status { get; set; } = string.Empty;
    public int? RemoteStatusCode { get; set; }
    [MaxLength(30)] public string? FailureReason { get; set; }

    /// <summary>A truncated slice of the remote's own response — the maker's data, not ours.</summary>
    [MaxLength(500)]
    public string? RemoteBodySnippet { get; set; }

    public int? LatencyMs { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public bool IsTest { get; set; }
}

/// <summary>
///     The console's non-delivery rows: key use, rate limiting, expiry, and players connecting.
///     Rate-limit and key-use rows are hourly roll-ups rather than per-request, so one runaway loop
///     cannot flood the table or the page.
/// </summary>
internal sealed class ToolActivityEntity
{
    [Key] public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    [Required] [MaxLength(40)] public string EventType { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The hour this row rolls up, for the aggregated kinds. Null for point events.</summary>
    public DateTimeOffset? WindowStart { get; set; }

    public int Count { get; set; }
    [MaxLength(200)] public string? Detail { get; set; }
}
