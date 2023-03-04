using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

public sealed record ReCalculateChartRatingCommand(Guid ChartId) : IRequest<ChartDifficultyRatingRecord>

{
}