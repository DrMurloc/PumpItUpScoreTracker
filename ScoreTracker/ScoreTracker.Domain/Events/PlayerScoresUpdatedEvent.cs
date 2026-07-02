namespace ScoreTracker.Domain.Events;

/// <summary>
///     Fat contract event: carries the Ledger's facts so consumers never reach back into
///     score storage. Payload is primitives-only — it must round-trip JSON cleanly because
///     it doubles as the partner webhook body (ADR-001 D3). Supersedes the thin
///     <see cref="PlayerScoreUpdatedEvent" />, which is dual-published during the P3
///     consumer migration and deleted at its end.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerScoresUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    Guid UserId,
    IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange> Changes)
{
    public const int CurrentSchemaVersion = 1;

    public static PlayerScoresUpdatedEvent Create(DateTimeOffset occurredAt, Guid userId,
        IReadOnlyList<ScoreChange> changes)
    {
        return new PlayerScoresUpdatedEvent(Guid.NewGuid(), occurredAt, CurrentSchemaVersion, userId, changes);
    }

    [ExcludeFromCodeCoverage]
    public sealed record ScoreChange(
        Guid ChartId,
        bool IsNewPass,
        int? OldScore,
        int? NewScore,
        string? Plate,
        bool IsBroken);
}
