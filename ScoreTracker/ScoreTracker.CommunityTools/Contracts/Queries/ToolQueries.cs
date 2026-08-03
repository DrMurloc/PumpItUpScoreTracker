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

/// <summary>Resolves a presented API key to its tool. The v2 auth scheme's only question.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolByApiKeyQuery(string Key) : IQuery<Guid?>;

/// <summary>Every player one tool may read, resolved across direct grants, the pool and blocks.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetToolReadablePlayersQuery(Guid ToolId) : IQuery<IReadOnlyList<Guid>>;

[ExcludeFromCodeCoverage]
public sealed record CanToolReadPlayerQuery(Guid ToolId, Guid UserId) : IQuery<bool>;

[ExcludeFromCodeCoverage]
public sealed record GetToolActivityQuery(Guid ToolId, int Limit = 100)
    : IQuery<IReadOnlyList<ToolActivityRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetToolActivitySummaryQuery(Guid ToolId) : IQuery<ToolActivitySummary>;
