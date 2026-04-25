using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record OfficialRecordedScore(Chart Chart, PhoenixScore Score, PhoenixPlate Plate,
        bool IsBroken = false)
    {
    }
}
