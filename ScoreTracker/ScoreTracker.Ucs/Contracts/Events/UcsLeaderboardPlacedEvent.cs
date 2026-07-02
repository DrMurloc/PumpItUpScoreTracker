namespace ScoreTracker.Ucs.Contracts.Events;

/// <summary>
///     Fat contract event: carries the UCS placement facts so consumers never reach back
///     into the UCS vertical's storage. Payload is primitives-only — it must round-trip
///     JSON cleanly because it doubles as a partner webhook body (ADR-001 D3). Score and
///     plate are the submitted values; UCS leaderboard entries overwrite unconditionally,
///     so these are also the stored values.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UcsLeaderboardPlacedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    Guid UserId,
    Guid ChartId,
    int Score,
    string Plate,
    bool IsBroken,
    string Artist,
    string SongName,
    string Difficulty)
{
    public const int CurrentSchemaVersion = 1;

    public static UcsLeaderboardPlacedEvent Create(DateTimeOffset occurredAt, Guid userId, Guid chartId,
        int score, string plate, bool isBroken, string artist, string songName, string difficulty)
    {
        return new UcsLeaderboardPlacedEvent(Guid.NewGuid(), occurredAt, CurrentSchemaVersion, userId, chartId,
            score, plate, isBroken, artist, songName, difficulty);
    }
}
