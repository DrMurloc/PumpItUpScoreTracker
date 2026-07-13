using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Posts the match's current state to the tournament's configured Discord channel.
    ///     Any staff role; manual only — a redraw never auto-pushes, and every push is a
    ///     fresh message so the channel keeps the draw's history.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PushDrawToDiscordCommand(Guid DrawId) : IRequest
    {
    }
}
