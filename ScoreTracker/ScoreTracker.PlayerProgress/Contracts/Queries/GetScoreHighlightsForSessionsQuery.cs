using MediatR;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Reads a player's captured noteworthy-score flags for specific sessions. FK straight
///     to SessionId rather than a date window: highlights are stamped at batch-drain,
///     minutes after their journal rows, so a row-time window drops the freshest session's
///     flags (the "Of Note shows nothing" bug).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetScoreHighlightsForSessionsQuery(Guid UserId, IReadOnlyCollection<Guid> SessionIds)
    : IQuery<IEnumerable<ScoreHighlightRecord>>;
