using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The caller's place/total on today's Daily Step board (null if they have no entry).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetDailyStepPlacementQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<DailyStepPlacement?>
{
}
