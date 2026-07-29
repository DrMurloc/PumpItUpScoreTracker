using MediatR;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>
    ///     Whether a tournament runs qualifiers at all. One flag, so a listing page does not need
    ///     the repository just to decide whether to show a link.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record TournamentHasQualifiersQuery(Guid TournamentId) : IQuery<bool>
    {
    }
}
