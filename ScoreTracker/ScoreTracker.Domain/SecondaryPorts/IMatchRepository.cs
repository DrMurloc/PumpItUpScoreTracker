using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IMatchRepository
{
    Task<MatchView> GetMatch(Guid tournamentId, Name matchName, CancellationToken cancellationToken);
    Task<IEnumerable<MatchView>> GetAllMatches(Guid tournamentId, CancellationToken cancellationToken);
    Task SaveMatch(Guid tournamentId, MatchView matchView, CancellationToken cancellationToken);

    Task SaveRandomSettings(Guid tournamentId, Name settingsName, RandomSettings settings,
        CancellationToken cancellationToken);

    Task<RandomSettings> GetRandomSettings(Guid tournamentId, Name settingsName, CancellationToken cancellationToken);

    Task<IEnumerable<(Name name, RandomSettings settings)>> GetAllRandomSettings(Guid tournamentId,
        CancellationToken cancellationToken);

    Task<IEnumerable<MatchLink>> GetMatchLinksByFromMatchName(Guid tournamentId, Name fromMatchName,
        CancellationToken cancellationToken);

    Task SaveMatchLink(Guid tournamentId, MatchLink matchLink, CancellationToken cancellationToken);
    Task DeleteMatchLink(Guid tournamentId, Name fromName, Name toName, CancellationToken cancellationToken);
    Task<IEnumerable<MatchLink>> GetAllMatchLinks(Guid tournamentId, CancellationToken cancellationToken);
    Task<IEnumerable<MatchPlayer>> GetMatchPlayers(Guid tournamentId, CancellationToken cancellationToken);
    Task SaveMatchPlayer(Guid tournamentId, MatchPlayer player, CancellationToken cancellationToken);
    Task DeleteMatchPlayer(Guid tournamentId, Name playerName, CancellationToken cancellationToken);
}