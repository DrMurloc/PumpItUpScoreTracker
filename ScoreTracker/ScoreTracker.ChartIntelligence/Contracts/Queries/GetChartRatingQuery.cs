using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartRatingQuery(MixEnum Mix, Guid ChartId) : IQuery<ChartDifficultyRatingRecord?>
{
}
