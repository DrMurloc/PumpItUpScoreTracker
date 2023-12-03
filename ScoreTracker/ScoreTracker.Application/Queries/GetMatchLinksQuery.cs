using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetMatchLinksQuery : IRequest<IEnumerable<MatchLink>>
    {
    }
}
