using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface ITournamentRepository
{
    Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken);
    Task<TournamentConfiguration> GetTournament(Guid id, CancellationToken cancellationToken);
    Task CreateOrSaveTournament(TournamentConfiguration tournament, CancellationToken cancellationToken);
    Task SaveSession(TournamentSession session, CancellationToken cancellationToken);
    Task<TournamentSession> GetSession(Guid tournamentId, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<LeaderboardRecord>> GetLeaderboardRecords(Guid tournamentId,
        CancellationToken cancellationToken);

    Task<IDictionary<Guid, double>?> GetScoringLevelSnapshot(Guid tournamentId,
        CancellationToken cancellationToken);

    Task SetRole(Guid tournamentId, Guid userId, TournamentRole role, CancellationToken cancellationToken);
    Task RevokeRole(Guid tournamentId, Guid userId, CancellationToken cancellationToken);
}