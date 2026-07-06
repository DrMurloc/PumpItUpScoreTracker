namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>A player's current place on one active weekly-board chart.</summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyPlacementRecord(Guid ChartId, int Place);
