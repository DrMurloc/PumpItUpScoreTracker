using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UserPhoenixScore(Guid UserId, Guid ChartId, Name UserName, PhoenixScore Score,
        PhoenixPlate? Plate,
        bool IsBroken)
    {
    }
}
