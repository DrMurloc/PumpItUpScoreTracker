using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface ITournamentRepository
{
    Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken);
    Task<TournamentConfiguration> GetTournament(Guid id, CancellationToken cancellationToken);
    Task CreateOrSaveTournament(TournamentConfiguration tournament, CancellationToken cancellationToken);
    Task CreateOrSaveTournament(TournamentRecord tournament, CancellationToken cancellationToken);
    Task SaveSession(TournamentSession session, CancellationToken cancellationToken);
    Task<TournamentSession> GetSession(Guid tournamentId, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<LeaderboardRecord>> GetLeaderboardRecords(Guid tournamentId,
        CancellationToken cancellationToken);

    Task CreateScoringLevelSnapshots(Guid tournamentId,
        IEnumerable<(Guid, double)> snapshots, CancellationToken cancellationToken);

    Task<IDictionary<Guid, double>?> GetScoringLevelSnapshot(Guid tournamentId,
        CancellationToken cancellationToken);

    Task SetRole(Guid tournamentId, Guid userId, TournamentRole role, CancellationToken cancellationToken);
    Task RevokeRole(Guid tournamentId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Micro-tournament creation (docs/design/randomizer-overhaul.md): the row is written
    ///     with IsUnlisted = true, so it never appears in GetAllTournaments — reachable only
    ///     through roles (GetMyTournamentsQuery) until an admin lists it.
    /// </summary>
    Task CreateUnlistedTournament(TournamentRecord tournament, CancellationToken cancellationToken);

    Task<Guid> CreateRoleInvite(Guid tournamentId, TournamentRole role, DateTimeOffset? expiresAt, Guid createdBy,
        CancellationToken cancellationToken);

    Task<TournamentRoleInviteRecord?> GetRoleInvite(Guid token, CancellationToken cancellationToken);
    Task<IEnumerable<TournamentRoleInviteRecord>> GetRoleInvites(Guid tournamentId, CancellationToken cancellationToken);
    Task DeleteRoleInvite(Guid token, CancellationToken cancellationToken);

    /// <summary>Discord channel the randomizer pushes draws into; null clears it.</summary>
    Task SetDiscordChannel(Guid tournamentId, ulong? channelId, CancellationToken cancellationToken);

    Task<ulong?> GetDiscordChannel(Guid tournamentId, CancellationToken cancellationToken);
}