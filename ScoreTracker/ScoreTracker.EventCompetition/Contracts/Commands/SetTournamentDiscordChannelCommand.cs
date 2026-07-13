using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Points the randomizer's Push to Discord at a channel. Head TO or site admin only; null clears it.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record SetTournamentDiscordChannelCommand(Guid TournamentId, ulong? ChannelId) : IRequest
    {
    }
}
