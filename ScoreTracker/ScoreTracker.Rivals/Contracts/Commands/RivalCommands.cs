using MediatR;

namespace ScoreTracker.Rivals.Contracts.Commands;

/// <summary>
///     Draw an arrow at somebody. Exactly one target — a site player you found in the picker, or a
///     board tag you found on the official one.
///     <para>
///         A tag that already belongs to an account is stored as the ACCOUNT, never as the tag
///         (docs/design/rivals.md D4), so the same person can never occupy both columns. Returns
///         the edge id.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AddRivalCommand(Guid? TargetUserId, string? TargetTag) : IRequest<Guid>;

/// <summary>
///     Drop one arrow. Used from both ends: your own roster, and the reverse list where you are
///     removing somebody else's arrow at you.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveRivalCommand(Guid EdgeId) : IRequest;

/// <summary>
///     Symmetric (D15): both arrows go, and neither party can draw another. Idempotent.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BlockRivalCommand(Guid BlockedUserId) : IRequest;

[ExcludeFromCodeCoverage]
public sealed record UnblockRivalCommand(Guid BlockedUserId) : IRequest;

/// <summary>
///     Mint a fresh code, killing the old link. Edges already made with it SURVIVE (D24) —
///     revoking a person is the reverse list's job, and a recycle that silently unfriended people
///     would be a different feature wearing this one's label.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecycleRivalInviteCodeCommand : IRequest<string>;

/// <summary>Redeem somebody's code: adds them as your rival. Returns the edge id.</summary>
[ExcludeFromCodeCoverage]
public sealed record RedeemRivalInviteCodeCommand(string Code) : IRequest<Guid>;
