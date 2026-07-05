using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>This week's submitted entries for a mix's board, optionally filtered to one chart.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyChartEntriesQuery(Guid? ChartId = null, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<WeeklyTournamentEntry>>
{
}
