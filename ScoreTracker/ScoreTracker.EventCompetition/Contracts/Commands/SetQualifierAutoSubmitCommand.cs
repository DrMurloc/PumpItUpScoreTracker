using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Turns auto-submit from imports on or off for the calling user.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record SetQualifierAutoSubmitCommand(Guid TournamentId, bool Enabled) : IRequest
    {
    }
}
