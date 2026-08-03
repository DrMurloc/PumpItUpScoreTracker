using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Contracts.Commands;

/// <summary>
///     Registers a tool. <paramref name="RepositoryUrl" /> and <paramref name="DiscordHandle" /> may
///     be blank — a maker building against their own scores needs neither — but without both the
///     tool can never reach a second player.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CreateToolCommand(string Name, string? RepositoryUrl = null,
    string? DiscordHandle = null) : IRequest<Guid>;

[ExcludeFromCodeCoverage]
public sealed record UpdateToolCommand(Guid ToolId, string Name, string? Description, string? Url,
    string? RepositoryUrl = null, string? DiscordHandle = null) : IRequest;

/// <summary>
///     Fetches the repository link anonymously and records whether it answered. A private repository
///     404s to exactly the players it is meant to be readable by, and looks identical to a typo.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CheckToolRepositoryCommand(Guid ToolId) : IRequest<RepositoryCheckResult>;

/// <summary>
///     The outcome, in the console's closed vocabulary plus whatever the remote actually said —
///     the same rule the webhook console follows, for the same reason.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RepositoryCheckResult(bool Reachable, string? Reason, int? StatusCode);

[ExcludeFromCodeCoverage]
public sealed record SetToolAllToolsShareCommand(Guid ToolId, bool Accepts) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record SetToolWebhookCommand(Guid ToolId, WebhookMode Mode, string? Url,
    IReadOnlyList<MixEnum> Mixes) : IRequest;

/// <summary>
///     Sets the header we send verbatim on every delivery, which is how a maker's server knows a
///     call is ours. A null or blank <paramref name="Value" /> keeps whatever is stored — the field
///     is a secret the maker chose, and a blank box on a settings form must not erase one.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetToolOutboundHeaderCommand(Guid ToolId, string? Name, string? Value) : IRequest;

/// <summary>
///     Sets the secret a maker's endpoint must answer a verification request with. Stored as a hash
///     and never sent anywhere — a blank value clears it, which also un-verifies the URL.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetToolVerificationSecretCommand(Guid ToolId, string? Secret) : IRequest;

/// <summary>
///     Proves the maker's endpoint is theirs: we POST a bare verification request and it answers
///     with the secret only they registered. Nothing is delivered anywhere until this succeeds.
///     <para>
///         The request deliberately carries no challenge. Echoing a value we just sent proves the
///         endpoint can read, not that it knows anything — so a hijacked DNS record passed the
///         earlier version of this by replying with our own bytes.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record VerifyToolWebhookCommand(Guid ToolId) : IRequest<WebhookVerificationResult>;

/// <summary>
///     The outcome, in the console's closed vocabulary plus whatever the remote actually said. No
///     exception text — a maker can act on "your server returned 404" and cannot act on ours.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WebhookVerificationResult(bool Verified, string? Reason, int? StatusCode,
    string? ResponseSnippet);

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
///     The maker's own note about where they shared one link. Blank clears it. Never leaves the
///     maker's console.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SetToolInviteLinkNoteCommand(Guid ToolId, Guid Code, string? Note) : IRequest;

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

/// <summary>
///     Bars a maker from making tools, and stops the ones they have.
///     <para>
///         Disables, never deletes. Their tools keep their shares, keys, activity log and delivery
///         history exactly as they are and simply read nobody — so a ban can be looked at afterwards,
///         and lifted, which a hard delete makes impossible. <see cref="Notes" /> is the admin's own
///         scratch space and is seen by nobody else.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BanToolMakerCommand(Guid UserId, string? Notes) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record LiftToolMakerBanCommand(Guid UserId) : IRequest;

/// <summary>Editable afterwards, so a ban can record how it ended as well as why it started.</summary>
[ExcludeFromCodeCoverage]
public sealed record SetToolMakerBanNotesCommand(Guid UserId, string? Notes) : IRequest;
