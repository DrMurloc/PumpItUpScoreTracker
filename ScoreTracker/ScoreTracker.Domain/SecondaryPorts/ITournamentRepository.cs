using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface ITournamentRepository
    {
        Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken);
        Task<TournamentConfiguration> GetTournament(Guid id, CancellationToken cancellationToken);
        Task CreateOrSaveTournament(TournamentConfiguration tournament, CancellationToken cancellationToken);
    }
}