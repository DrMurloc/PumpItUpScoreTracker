using MediatR;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SetApiTokenCommand : IRequest<Guid>
    {
    }
}
