using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Events
{
    public sealed record ChartDifficultyUpdatedEvent(ChartType ChartType, int Level)
    {
    }
}
