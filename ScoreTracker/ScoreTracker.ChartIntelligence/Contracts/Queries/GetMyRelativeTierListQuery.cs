using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    public sealed record GetMyRelativeTierListQuery
        (ChartType ChartType, DifficultyLevel Level, Guid? UserId = null) : IQuery<IEnumerable<SongTierListEntry>>
    {
    }
}
