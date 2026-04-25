using MediatR;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetUserApiTokenQuery : IRequest<Guid?>
    {
    }
}
