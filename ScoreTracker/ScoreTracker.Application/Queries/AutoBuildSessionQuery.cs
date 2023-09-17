using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    public sealed record AutoBuildSessionQuery
    (TournamentConfiguration Configuration, Guid UserId,
        TimeSpan MinimumRestPerChart) : IRequest<TournamentSession>
    {
    }
}