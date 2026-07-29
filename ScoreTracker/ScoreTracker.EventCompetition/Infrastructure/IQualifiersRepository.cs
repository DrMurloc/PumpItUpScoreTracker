using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Infrastructure
{
    /// <summary>
    ///     Qualifier persistence. Vertical-internal: only EventCompetition implements it and
    ///     only EventCompetition consumes it, now that the pages dispatch through MediatR.
    /// </summary>
    internal interface IQualifiersRepository
    {
        Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Name userName, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Guid userId, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task SaveQualifiers(Guid tournamentId, UserQualifiers qualifiers,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Removes an entrant's whole entry. Organiser action — this is how a duplicate gets
        ///     cleared. History snapshots are left alone.
        /// </summary>
        Task DeleteQualifiers(Guid tournamentId, Name userName, CancellationToken cancellationToken = default);

        /// <summary>When each entrant's entry was first recorded, keyed by entry name.</summary>
        Task<IDictionary<string, DateTimeOffset>> GetFirstSubmissionDates(Guid tournamentId,
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

        /// <summary>Turns auto-submit back off — the direction registration never had.</summary>
        Task UnregisterUserFromTournament(Guid tournamentId, Guid userId,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Guid>> GetRegisteredUsers(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Guid>> GetRegisteredTournaments(Guid userId, CancellationToken cancellationToken = default);
    }
}
