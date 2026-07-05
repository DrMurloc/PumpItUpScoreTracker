using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>Chart ids that have already appeared on a mix's past weekly boards (rotation exclusions).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetAlreadyPlayedWeeklyChartsQuery(MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<Guid>>
{
}
