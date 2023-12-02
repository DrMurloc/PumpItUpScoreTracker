using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    public sealed record UpdateMatchCommand(MatchView NewView) : IRequest
    {
    }
}
