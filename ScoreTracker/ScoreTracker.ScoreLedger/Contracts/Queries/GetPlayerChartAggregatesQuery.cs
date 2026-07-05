using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     ChartMix filters the mix a chart DEBUTED in; Mix is the mix the records were scored
///     under (the parallel-mix key) — deliberately distinct semantics.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerChartAggregatesQuery(MixEnum? ChartMix = null, Name? CommunityName = null,
    DifficultyLevel? MinLevel = null, DifficultyLevel? MaxLevel = null,
    ChartType? ChartType = null, MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<UserChartAggregate>>
{
}
