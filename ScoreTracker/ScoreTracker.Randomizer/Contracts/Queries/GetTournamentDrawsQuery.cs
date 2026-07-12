using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    /// <summary>
    ///     A tournament's matches, newest first. Anonymous by design — the spectate page
    ///     uses it for its match tabs, so anyone holding one match link can follow the
    ///     night's others.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentDrawsQuery(Guid TournamentId) : IQuery<IEnumerable<DrawDto>>
    {
    }
}
