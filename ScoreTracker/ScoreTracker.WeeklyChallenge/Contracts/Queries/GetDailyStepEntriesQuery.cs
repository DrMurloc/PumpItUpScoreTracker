using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>All player entries on today's Daily Step chart for a mix, each with its source.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetDailyStepEntriesQuery(MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<DailyStepEntry>>
{
}
