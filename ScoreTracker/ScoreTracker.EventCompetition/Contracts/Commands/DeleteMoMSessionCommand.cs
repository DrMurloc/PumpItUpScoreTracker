using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Deletes a session and its chart rows. On a draft this is Discard; on a published
///     session it is the D17 correction path (delete-and-resubmit — recorded date moves,
///     which only affects tie-breaks). Owner or admin only; an already-sent Discord card is
///     left to 404 (§10).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteMoMSessionCommand(Guid SessionId) : IRequest;
