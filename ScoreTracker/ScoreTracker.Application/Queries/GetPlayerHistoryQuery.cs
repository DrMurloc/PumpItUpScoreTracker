using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerHistoryQuery(Guid UserId) : IQuery<IEnumerable<PlayerRatingRecord>>
    {
    }
}
