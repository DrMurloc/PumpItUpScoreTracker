using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record RecordedPhoenixScore(Guid ChartId, PhoenixScore? Score, PhoenixPlate? Plate,
    bool IsBroken, DateTimeOffset RecordedDate)
{
}