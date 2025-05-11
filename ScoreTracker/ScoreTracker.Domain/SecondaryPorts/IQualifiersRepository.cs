using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IQualifiersRepository
    {
        Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Name userName, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Guid userId, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task SaveQualifiers(Guid tournamentId, UserQualifiers qualifiers,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<UserQualifiers>> GetAllUserQualifiers(Guid tournamentId, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task<QualifiersConfiguration> GetQualifiersConfiguration(Guid tournamentId,
            CancellationToken cancellationToken = default);

        Task SaveTeam(Guid tournamentId, CoOpTeam team, CancellationToken cancellationToken = default);
        Task SaveIndividualPlayer(Guid tournamentId, CoOpPlayer player, CancellationToken cancellationToken = default);

        Task<IEnumerable<CoOpPlayer>> GetIndividualCoopPlayers(Guid tournamentId,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<CoOpTeam>> GetCoOpTeams(Guid tournamentId, CancellationToken cancellationToken = default);
        Task RegisterUserToTournament(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Guid>> GetRegisteredUsers(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Guid>> GetRegisteredTournaments(Guid userId, CancellationToken cancellationToken = default);
    }
}
