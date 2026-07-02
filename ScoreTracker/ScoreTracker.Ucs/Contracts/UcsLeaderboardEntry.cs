using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Ucs.Contracts;

[ExcludeFromCodeCoverage]
public sealed record UcsLeaderboardEntry(Guid UserId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
    Uri? VideoPath,
    Uri? ImagePath)
{
}
