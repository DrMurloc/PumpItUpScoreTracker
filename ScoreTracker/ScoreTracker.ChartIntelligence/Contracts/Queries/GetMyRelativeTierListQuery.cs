using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    public sealed record GetMyRelativeTierListQuery
        (ChartType ChartType, DifficultyLevel Level, Guid? UserId = null, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<SongTierListEntry>>
    {
    }
}
