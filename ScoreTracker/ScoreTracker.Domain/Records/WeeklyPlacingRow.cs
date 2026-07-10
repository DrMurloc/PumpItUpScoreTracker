namespace ScoreTracker.Domain.Records;

/// <summary>
///     One archived weekly-board placing: who scored what on which chart the week ending
///     at <paramref name="ObtainedDate" />, with the competitive level and range flag
///     snapshotted at week close.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyPlacingRow(
    Guid UserId,
    Guid ChartId,
    DateTimeOffset ObtainedDate,
    int Place,
    int Score,
    bool IsBroken,
    bool WasWithinRange,
    double CompetitiveLevel);
