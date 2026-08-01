using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>The player's sessions, newest first — what the Undo page lists.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetScoreSessionsQuery(Guid UserId) : IQuery<IReadOnlyList<ScoreSessionRecord>>;
