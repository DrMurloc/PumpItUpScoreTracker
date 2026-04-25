using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMatchLinksQuery(Guid TournamentId) : IRequest<IEnumerable<MatchLink>>
    {
    }
}
