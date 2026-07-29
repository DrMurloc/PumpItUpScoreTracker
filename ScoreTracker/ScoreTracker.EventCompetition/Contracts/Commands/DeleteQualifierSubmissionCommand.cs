using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Organiser action: drop one chart from an entry, leaving the rest.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record DeleteQualifierSubmissionCommand(Guid TournamentId, Name UserName, Guid ChartId) : IRequest
    {
    }
}
