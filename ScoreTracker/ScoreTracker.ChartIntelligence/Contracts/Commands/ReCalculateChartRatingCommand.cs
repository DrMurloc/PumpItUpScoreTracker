using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record ReCalculateChartRatingCommand(MixEnum Mix, Guid ChartId) : IRequest<ChartDifficultyRatingRecord>

{
}
