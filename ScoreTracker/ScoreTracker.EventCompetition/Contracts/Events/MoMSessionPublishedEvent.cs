namespace ScoreTracker.EventCompetition.Contracts.Events;

/// <summary>
///     A session was published to a board (D17 — exactly once per session; a published
///     session cannot be edited, so no second event ever corrects a first). Consumers
///     re-read what they render — the Discord card reads the session and its board
///     placement at delivery time.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionPublishedEvent(Guid SessionId, Guid BoardId, Guid UserId);
