using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record ReCalculateChartRatingCommand(MixEnum Mix, Guid ChartId) : IRequest<ChartDifficultyRatingRecord>

{
}
