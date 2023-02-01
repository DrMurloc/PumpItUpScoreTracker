using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartRatingQuery(Guid ChartId) : IRequest<ChartDifficultyRatingRecord?>
{
}