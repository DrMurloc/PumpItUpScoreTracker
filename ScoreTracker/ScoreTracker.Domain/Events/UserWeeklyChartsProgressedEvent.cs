using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Events
{
    public sealed record UserWeeklyChartsProgressedEvent(Guid UserId, Guid ChartId, PhoenixScore Score,
        PhoenixPlate Plate, bool IsBroken, int Place)
    {
    }
}
