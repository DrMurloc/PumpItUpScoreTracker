using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateToolCommand(string Name) : IRequest<Guid>;

[ExcludeFromCodeCoverage]
public sealed record UpdateToolCommand(Guid ToolId, string Name, string? Description, string? Url)
    : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetToolAllToolsShareCommand(Guid ToolId, bool Accepts) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetToolWebhookCommand(Guid ToolId, WebhookMode Mode, string? Url,
    IReadOnlyList<MixEnum> Mixes) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record RequestToolListingCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record ApproveToolCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record RejectToolCommand(Guid ToolId, string Reason) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record DeleteToolCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record CreateToolApiKeyCommand(Guid ToolId, string Name, DateTimeOffset? ExpiresAt)
    : IRequest<MintedApiKey>;

[ExcludeFromCodeCoverage]
public sealed record RevokeToolApiKeyCommand(Guid ToolId, Guid KeyId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record CreateToolInviteLinkCommand(Guid ToolId) : IRequest<Guid>;

[ExcludeFromCodeCoverage]
public sealed record RevokeToolInviteLinkCommand(Guid ToolId, Guid Code) : IRequest;

/// <summary>A player granting one named tool access, whether from the directory or an invite.</summary>
[ExcludeFromCodeCoverage]
public sealed record ConnectToolCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record DisconnectToolCommand(Guid ToolId) : IRequest;

/// <summary>An all-tools player refusing one tool without turning off sharing entirely.</summary>
[ExcludeFromCodeCoverage]
public sealed record BlockToolCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetShareWithAllToolsCommand(bool Share) : IRequest;
