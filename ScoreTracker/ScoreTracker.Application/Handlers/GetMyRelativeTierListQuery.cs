using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed record GetMyRelativeTierListQuery
        (ChartType ChartType, DifficultyLevel Level, Guid? UserId = null) : IRequest<IEnumerable<SongTierListEntry>>
    {
    }
}
