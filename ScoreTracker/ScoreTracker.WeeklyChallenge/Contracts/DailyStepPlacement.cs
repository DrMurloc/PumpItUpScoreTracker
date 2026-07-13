namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     The caller's standing on today's Daily Step board. On a Limbo day, Total counts only passing
///     entrants and Place ranks lowest-passing-first.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepPlacement(int Place, int Total, bool IsLimbo);
