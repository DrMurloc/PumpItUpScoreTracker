using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetAllRandomSettingsQuery
        (Guid TournamentId) : IRequest<IEnumerable<(Name name, RandomSettings settings)>>
    {
    }
}
