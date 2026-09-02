namespace ScoreTracker.Domain.Records;

/// <summary>
///     Where the bot's gateway socket stands. Connecting and Disconnecting collapse into
///     <see cref="Disconnected" />: the only thing the watchdog asks is whether commands can
///     arrive right now, and for how long they have not been able to.
/// </summary>
public enum BotGatewayState
{
    NotStarted,
    Connected,
    Disconnected
}

/// <summary>
///     A reading of the bot's gateway connection. <paramref name="DisconnectedFor" /> is how
///     long the socket has continuously been out of the Connected state; zero unless
///     <paramref name="State" /> is <see cref="BotGatewayState.Disconnected" />.
///     Slash commands arrive over the gateway, so a disconnected socket means no command can
///     reach the bot even while REST sends keep working (docs/design/discord-overhaul.md §10).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotGatewayStatus(BotGatewayState State, TimeSpan DisconnectedFor)
{
    public static readonly BotGatewayStatus NotStarted = new(BotGatewayState.NotStarted, TimeSpan.Zero);
    public static readonly BotGatewayStatus Connected = new(BotGatewayState.Connected, TimeSpan.Zero);

    public static BotGatewayStatus DisconnectedSince(TimeSpan elapsed)
    {
        return new BotGatewayStatus(BotGatewayState.Disconnected, elapsed);
    }
}
