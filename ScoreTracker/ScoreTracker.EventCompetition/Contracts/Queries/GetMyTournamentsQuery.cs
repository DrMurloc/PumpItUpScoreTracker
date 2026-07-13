using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>
    ///     Every tournament the current user holds a role in — including unlisted
    ///     micro-tournaments, which appear nowhere else. Empty when logged out.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetMyTournamentsQuery : IQuery<IEnumerable<TournamentRoleListing>>
    {
    }
}
