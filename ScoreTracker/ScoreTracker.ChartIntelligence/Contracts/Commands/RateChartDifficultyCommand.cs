using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RateChartDifficultyCommand
    (MixEnum Mix, Guid ChartId, DifficultyAdjustment Rating) : IRequest<ChartDifficultyRatingRecord>
{
}
