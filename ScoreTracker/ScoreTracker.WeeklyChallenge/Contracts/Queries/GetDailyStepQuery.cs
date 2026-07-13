using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>Today's live Daily Step board for a mix (null when none is live yet).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetDailyStepQuery(MixEnum Mix = MixEnum.Phoenix) : IQuery<DailyStepBoard?>
{
}
