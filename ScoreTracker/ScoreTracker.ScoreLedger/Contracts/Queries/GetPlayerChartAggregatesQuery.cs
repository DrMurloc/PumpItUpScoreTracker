using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPlayerChartAggregatesQuery(MixEnum? ChartMix = null, Name? CommunityName = null,
    DifficultyLevel? MinLevel = null, DifficultyLevel? MaxLevel = null,
    ChartType? ChartType = null) : IQuery<IEnumerable<UserChartAggregate>>
{
}
