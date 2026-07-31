using MediatR;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Removes one session and puts the affected charts back to what the remaining plays
///     produce. Surgical, not a rewind: every other session is untouched, including newer ones,
///     and sessions can be undone in any order.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UndoScoreSessionCommand(Guid UserId, Guid SessionId, bool ForgetCredential = false)
    : IRequest<ScoreSessionUndoResult>;
