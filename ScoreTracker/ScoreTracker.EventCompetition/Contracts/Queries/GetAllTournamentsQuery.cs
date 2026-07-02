using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetAllTournamentsQuery : IQuery<IEnumerable<TournamentRecord>>
    {
    }
}
