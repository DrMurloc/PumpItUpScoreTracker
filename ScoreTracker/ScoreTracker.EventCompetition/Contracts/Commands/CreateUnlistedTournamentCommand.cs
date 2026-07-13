using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>
    ///     Creates a micro-tournament (docs/design/randomizer-overhaul.md): unlisted, no
    ///     dates, creator becomes Head Tournament Organizer. The caller supplies the id
    ///     (established SaveTournamentCommand convention — handlers never mint ids).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CreateUnlistedTournamentCommand(Guid TournamentId, Name Name) : IRequest
    {
    }
}
