using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTop50ForPlayerQuery
        (Guid UserId, ChartType? ChartType, int Count = 50, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
