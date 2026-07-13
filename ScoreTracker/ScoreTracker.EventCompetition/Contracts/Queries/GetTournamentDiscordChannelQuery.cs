using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>The configured push channel, if any. Any staff role (they see the push button); never anonymous.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentDiscordChannelQuery(Guid TournamentId) : IQuery<ulong?>
    {
    }
}
