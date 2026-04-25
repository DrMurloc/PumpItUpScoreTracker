using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartRatingQuery(MixEnum Mix, Guid ChartId) : IRequest<ChartDifficultyRatingRecord?>
{
}
