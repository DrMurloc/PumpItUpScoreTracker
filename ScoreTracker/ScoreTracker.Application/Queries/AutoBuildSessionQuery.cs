using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record AutoBuildSessionQuery
    (TournamentConfiguration Configuration, Guid UserId,
        TimeSpan MinimumRestPerChart) : IRequest<TournamentSession>
    {
    }
}
