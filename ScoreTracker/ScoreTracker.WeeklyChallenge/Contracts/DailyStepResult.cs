using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Contracts
{
    /// <summary>One finished-board placement, carried on <c>DailyStepRotatedEvent</c> for the Discord feed.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record DailyStepResult(int Place, Guid UserId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken);
}
