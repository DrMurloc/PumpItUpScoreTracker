using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SetApiTokenCommand : IRequest<Guid>
    {
    }
}
