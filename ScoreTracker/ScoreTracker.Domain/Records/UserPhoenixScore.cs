using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UserPhoenixScore(Guid UserId, Guid ChartId, Name UserName, PhoenixScore Score,
        PhoenixPlate? Plate,
        bool IsBroken)
    {
    }
}
