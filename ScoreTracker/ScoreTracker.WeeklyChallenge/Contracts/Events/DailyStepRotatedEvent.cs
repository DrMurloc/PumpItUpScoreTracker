using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Events
{
    /// <summary>
    ///     Published when a Daily Step board rotates for a mix. Carries the just-finished
    ///     board's placements inline (there is no placement-history read otherwise); the
    ///     new day's chart is read from <c>GetDailyStepQuery</c>. Empty placements =
    ///     nothing finished (first board), so the feed shows only today's chart.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record DailyStepRotatedEvent(
        MixEnum Mix,
        Guid FinishedChartId,
        DateTimeOffset FinishedForDate,
        bool FinishedIsLimbo,
        IReadOnlyList<DailyStepResult> FinishedPlacements);
}
