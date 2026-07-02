using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCompetitivePlayersQuery(ChartType ChartType) : IQuery<IEnumerable<Guid>>
    {
    }
}
