using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartRatingsQuery
    (MixEnum Mix, DifficultyLevel? Level = null, ChartType? Type = null) : IQuery<
        IEnumerable<ChartDifficultyRatingRecord>>
{
}
