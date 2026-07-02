using MediatR;

namespace ScoreTracker.Identity.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetUserApiTokenQuery : IQuery<Guid?>
    {
    }
}
