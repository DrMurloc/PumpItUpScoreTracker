using MediatR;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteMatchLinkCommand(Guid LinkId) : IRequest
    {
    }
}
