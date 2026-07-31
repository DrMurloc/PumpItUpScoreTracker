using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    // Plate is null on a broken entry — no plate is awarded for a failed stage.
    public sealed record UserWeeklyChartsProgressedEvent(Guid UserId, Guid ChartId, int Score,
        string? Plate, bool IsBroken, int Place, MixEnum Mix)
    {
    }
}
