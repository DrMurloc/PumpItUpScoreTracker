using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Opens a draft on a board, or resumes the one already open: a player holds at most one at a
///     time, so "New session" never piles up half-entered rows. Returns the session id.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CreateMoMDraftCommand(Guid BoardId) : IRequest<Guid>;
