using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     One finished day of a player's Daily Step history (from the placings snapshot the
///     rotation writes — daily-step.md L6's retained history, first surfaced by the challenges
///     page). <c>TotalPlayers</c> is how many placed on that day's board.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepHistoryRecord(
    DateTimeOffset ForDate,
    Guid ChartId,
    bool IsLimbo,
    int Place,
    int TotalPlayers,
    PhoenixScore Score,
    PhoenixPlate Plate,
    bool IsBroken);
