using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPlayerChartAggregatesQuery(MixEnum? ChartMix = null, Name? CommunityName = null,
    DifficultyLevel? MinLevel = null, DifficultyLevel? MaxLevel = null,
    ChartType? ChartType = null) : IQuery<IEnumerable<UserChartAggregate>>
{
}
