using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record UserTourneyHistory(Guid UserId, Guid ChartId, DateTimeOffset ReceivedOn, int Place,
    double CompetitiveLevel,
    PhoenixScore Score, PhoenixPlate Plate, bool IsBroken)
{
}
