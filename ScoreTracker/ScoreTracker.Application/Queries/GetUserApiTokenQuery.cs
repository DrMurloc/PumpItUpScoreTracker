using MediatR;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetUserApiTokenQuery : IRequest<Guid?>
    {
    }
}
