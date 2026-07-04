using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The dates of a mix's archived weekly boards.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPastWeeklyDatesQuery(MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<DateTimeOffset>>
{
}
