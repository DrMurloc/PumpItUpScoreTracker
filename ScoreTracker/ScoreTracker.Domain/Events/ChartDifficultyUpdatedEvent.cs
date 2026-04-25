using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record ChartDifficultyUpdatedEvent(ChartType ChartType, int Level)
    {
    }
}
