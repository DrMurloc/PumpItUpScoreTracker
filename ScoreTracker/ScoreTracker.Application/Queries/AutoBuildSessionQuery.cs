using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    public sealed record AutoBuildSessionQuery
    (StaminaSessionConfiguration Configuration, Guid UserId,
        TimeSpan MinimumRestPerChart) : IRequest<StaminaSession>
    {
    }
}