using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerStatsQuery(Guid UserId) : IRequest<PlayerStatsRecord>
    {
    }
}
