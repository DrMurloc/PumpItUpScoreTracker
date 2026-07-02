using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerHistoryQuery(Guid UserId) : IQuery<IEnumerable<PlayerRatingRecord>>
    {
    }
}
