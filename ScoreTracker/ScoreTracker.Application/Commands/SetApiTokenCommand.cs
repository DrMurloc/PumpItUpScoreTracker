using MediatR;

namespace ScoreTracker.Application.Commands
{
    public sealed record SetApiTokenCommand : IRequest<Guid>
    {
    }
}
