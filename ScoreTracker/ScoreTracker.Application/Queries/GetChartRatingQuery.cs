using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartRatingQuery(MixEnum Mix, Guid ChartId) : IRequest<ChartDifficultyRatingRecord?>
{
}