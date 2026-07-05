using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerStatsQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IQuery<PlayerStatsRecord>
    {
    }
}
