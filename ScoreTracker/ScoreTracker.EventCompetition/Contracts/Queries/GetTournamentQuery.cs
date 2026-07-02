using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentQuery(Guid TournamentId) : IQuery<TournamentConfiguration>
    {
    }
}
