using ScoreTracker.Domain.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartScoringLevelsQuery(MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IDictionary<Guid, double>>
{
}
