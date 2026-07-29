using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    /// <summary>
    ///     A player's score on one weekly chart went up, or landed for the first time, and moved
    ///     their place on that chart's board. Never published for a correction downward
    ///     (weekly-charts-overhaul.md §9.5) — consumers turn this into milestones, and falling is
    ///     not one. The name states that guarantee because it is the whole contract.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record UserWeeklyChartScoreImprovedEvent(Guid UserId, Guid ChartId, int Score,
        string Plate, bool IsBroken, int Place, MixEnum Mix)
    {
    }
}
