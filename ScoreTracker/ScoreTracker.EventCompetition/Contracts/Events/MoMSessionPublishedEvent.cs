namespace ScoreTracker.EventCompetition.Contracts.Events;

/// <summary>
///     A session reached a board. Published on the bus so slice 4c's Discord card is additive: a
///     draft never fires this, and a deleted session's card is left to 404 (§10).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionPublishedEvent(Guid SessionId, Guid BoardId, Guid UserId,
    DateTimeOffset PublishedAt);
