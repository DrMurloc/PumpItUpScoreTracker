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

/// <summary>
///     A player granting one named tool access, whether from the directory or an invite.
///     <para>
///         <see cref="AcceptedSessionSharing" /> is the consent the player actually gave, carried
///         back so the handler can check it still matches what the tool asks for. A maker can move a
///         tool into PIUGame-session mode the moment its last player disconnects — including in the
///         seconds between a player opening the connect dialog and pressing the button. Without
///         this, that player consented to score reads and granted piugame.com account control.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ConnectToolCommand(Guid ToolId, bool AcceptedSessionSharing = false) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record DisconnectToolCommand(Guid ToolId) : IRequest;

/// <summary>An all-tools player refusing one tool without turning off sharing entirely.</summary>
[ExcludeFromCodeCoverage]
public sealed record BlockToolCommand(Guid ToolId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetShareWithAllToolsCommand(bool Share) : IRequest;

/// <summary>
///     Fires a real, signed delivery at the tool's own endpoint.
///     <para>
///         Always marked as a test, and always about the maker's own account — a test can never
///         carry another player's scores into someone's production database.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SendTestDeliveryCommand(Guid ToolId, MixEnum Mix, bool UseMyLastImport,
    int SyntheticCount = 10) : IRequest;

/// <summary>Re-sends a past delivery whose body we still hold.</summary>
[ExcludeFromCodeCoverage]
public sealed record ReplayDeliveryCommand(Guid ToolId, Guid DeliveryRowId) : IRequest<bool>;
