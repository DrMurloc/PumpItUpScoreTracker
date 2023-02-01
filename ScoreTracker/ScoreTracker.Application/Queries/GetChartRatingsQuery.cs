using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartRatingsQuery : IRequest<IEnumerable<ChartDifficultyRatingRecord>>
{
}