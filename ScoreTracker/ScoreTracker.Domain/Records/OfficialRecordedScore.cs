using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record OfficialRecordedScore(Chart Chart, PhoenixScore Score, PhoenixPlate Plate)
    {
    }
}
