using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCompetitivePlayersQuery(ChartType ChartType, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<Guid>>
    {
    }
}
