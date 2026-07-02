using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record OfficialRecordedScore(Chart Chart, PhoenixScore Score, PhoenixPlate Plate,
        bool IsBroken = false)
    {
    }
}
