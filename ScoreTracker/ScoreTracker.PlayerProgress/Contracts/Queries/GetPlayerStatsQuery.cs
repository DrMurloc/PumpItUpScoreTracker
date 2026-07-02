using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerStatsQuery(Guid UserId) : IQuery<PlayerStatsRecord>
    {
    }
}
