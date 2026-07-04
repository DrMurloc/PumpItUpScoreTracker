using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The current week's challenge board charts for a mix (parallel boards per mix).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyChartsQuery(MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<WeeklyTournamentChart>>
{
}
