using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record RecordedPhoenixScore(Guid ChartId, PhoenixScore? Score, PhoenixPlate? Plate,
    bool IsBroken, DateTimeOffset RecordedDate)
{
}