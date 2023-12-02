using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    public sealed record GetAllMatchesQuery : IRequest<IEnumerable<MatchView>>
    {
    }
}
