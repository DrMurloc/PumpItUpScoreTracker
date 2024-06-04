using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetPlayerHistoryQuery(Guid UserId) : IRequest<IEnumerable<PlayerRatingRecord>>
    {
    }
}
