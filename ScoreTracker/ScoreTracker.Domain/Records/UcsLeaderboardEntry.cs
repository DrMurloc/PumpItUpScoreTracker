using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UcsLeaderboardEntry(Guid UserId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
        Uri? VideoPath,
        Uri? ImagePath)
    {
    }
}
