using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    public sealed record CreateMatchLinkCommand(MatchLink MatchLink) : IRequest
    {
    }
}
