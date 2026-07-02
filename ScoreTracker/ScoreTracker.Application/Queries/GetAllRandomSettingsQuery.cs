using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetAllRandomSettingsQuery
        (Guid TournamentId) : IQuery<IEnumerable<(Name name, RandomSettings settings)>>
    {
    }
}
