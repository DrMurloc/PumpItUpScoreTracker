using ScoreTracker.CommunityTools.Contracts;

namespace ScoreTracker.CommunityTools.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetMyToolsQuery : IQuery<IReadOnlyList<ToolRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolQuery(Guid ToolId) : IQuery<ToolRecord?>;

/// <summary>
///     Every tool on the site, for the admin console. Returns empty for everyone else rather than
///     throwing — the caller is a page deciding whether to render a section, not a maker who did
///     something wrong.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetAllToolsQuery : IQuery<IReadOnlyList<ToolRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetPublicToolsQuery : IQuery<IReadOnlyList<PublicToolRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolsAwaitingReviewQuery : IQuery<IReadOnlyList<ToolRecord>>;

/// <summary>Every tool that can currently read the calling player's scores.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyToolConnectionsQuery : IQuery<IReadOnlyList<PlayerToolConnectionRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetShareWithAllToolsQuery : IQuery<bool>;

[ExcludeFromCodeCoverage]
public sealed record GetToolApiKeysQuery(Guid ToolId) : IQuery<IReadOnlyList<ApiKeyRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolInviteLinksQuery(Guid ToolId) : IQuery<IReadOnlyList<ToolInviteLinkRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolInvitePreviewQuery(Guid Code) : IQuery<ToolInvitePreview?>;

/// <summary>
///     Resolves a presented API key to its tool and the key's name. The v2 auth scheme's only
///     question; null for a key that is unknown, revoked or expired.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolByApiKeyQuery(string Key) : IQuery<ToolKeyPrincipal?>;

/// <summary>Every player one tool may read, resolved across direct grants, the pool and blocks.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolReadablePlayersQuery(Guid ToolId) : IQuery<IReadOnlyList<Guid>>;

[ExcludeFromCodeCoverage]
public sealed record CanToolReadPlayerQuery(Guid ToolId, Guid UserId) : IQuery<bool>;

/// <summary>
///     The account that owns a tool, or null for no such tool. Ungated, unlike
///     <see cref="GetToolQuery" />, which resolves the caller through the signed-in user — a
///     request made with a tool key has none, and the key already proves the caller acts for the
///     tool. The API uses it to answer "may this tool see that private community" with the maker's
///     own membership.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolOwnerQuery(Guid ToolId) : IQuery<Guid?>;

[ExcludeFromCodeCoverage]
public sealed record GetToolActivityQuery(Guid ToolId, int Limit = 100)
    : IQuery<IReadOnlyList<ToolActivityRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolActivitySummaryQuery(Guid ToolId) : IQuery<ToolActivitySummary>;

/// <summary>Every maker currently barred from making tools. Admin surfaces only.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolMakerBansQuery : IQuery<IReadOnlyList<ToolMakerBanRecord>>;

[ExcludeFromCodeCoverage]
public sealed record IsToolMakerBannedQuery(Guid UserId) : IQuery<bool>;

/// <summary>Everything a code sample needs substituted into it, for one tool.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolCodeSamplesQuery(Guid ToolId) : IQuery<ToolCodeContext>;
