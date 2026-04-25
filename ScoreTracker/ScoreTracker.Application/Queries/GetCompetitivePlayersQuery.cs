using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCompetitivePlayersQuery(ChartType ChartType) : IRequest<IEnumerable<Guid>>
    {
    }
}
