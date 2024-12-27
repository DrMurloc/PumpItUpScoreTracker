using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetPlayerChartAggregatesQuery(MixEnum? ChartMix = null, Name? CommunityName = null,
    DifficultyLevel? MinLevel = null, DifficultyLevel? MaxLevel = null,
    ChartType? ChartType = null) : IRequest<IEnumerable<UserChartAggregate>>
{
}