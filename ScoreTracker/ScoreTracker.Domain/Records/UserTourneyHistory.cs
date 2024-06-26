using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public sealed record UserTourneyHistory(Guid UserId, Guid ChartId, DateTimeOffset ReceivedOn, int Place,
    double CompetitiveLevel,
    PhoenixScore Score, PhoenixPlate Plate, bool IsBroken)
{
}