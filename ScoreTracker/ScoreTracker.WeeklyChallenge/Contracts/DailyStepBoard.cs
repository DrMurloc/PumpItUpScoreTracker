namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     Today's live Daily Step chart for a mix (0–1 per mix). The public shape shared by the
///     repository read and <c>GetDailyStepQuery</c>. <c>IsLimbo</c> flips the board to
///     lowest-passing-score-wins; <c>ExpirationDate</c> is the next midnight-ET reset.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepBoard(Guid ChartId, DateTimeOffset ForDate, bool IsLimbo,
    DateTimeOffset ExpirationDate);
