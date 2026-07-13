using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    /// <summary>
    ///     The context's current draw: null TournamentId = the current user's personal
    ///     draw. Null result = never drawn (the empty state).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetActiveDrawQuery(Guid? TournamentId) : IQuery<DrawDto?>
    {
    }
}
