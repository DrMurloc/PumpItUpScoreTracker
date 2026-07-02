using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartScoringLevelsQuery(MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IDictionary<Guid, double>>
{
}
