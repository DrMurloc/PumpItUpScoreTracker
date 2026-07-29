using MediatR;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>The player-facing board. Never carries photo URLs.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetQualifiersBoardQuery(Guid TournamentId) : IQuery<QualifierBoard>
    {
    }
}
