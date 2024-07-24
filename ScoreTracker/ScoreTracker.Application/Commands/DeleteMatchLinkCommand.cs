using MediatR;

namespace ScoreTracker.Application.Commands
{
    public sealed record DeleteMatchLinkCommand(Guid LinkId) : IRequest
    {
    }
}
