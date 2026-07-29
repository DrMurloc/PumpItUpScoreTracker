using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Organiser action: drop an entrant's whole entry. Not undoable.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record DeleteQualifierEntryCommand(Guid TournamentId, Name UserName) : IRequest
    {
    }
}
