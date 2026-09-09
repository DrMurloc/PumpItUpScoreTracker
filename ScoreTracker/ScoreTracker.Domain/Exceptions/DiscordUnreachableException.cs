namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     Discord could not be read at all — the bot is gone from the server, or the gateway is down
///     mid-reconnect.
///     <para>
///         Its own type because the alternative is worse than an error: a read that comes back
///         empty looks exactly like a server with nobody in it, so a caller would take an outage
///         for "nobody holds this role", do nothing, and report success. Taking a mapping away on
///         the back of that leaves the role on everybody with nothing left that manages it.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DiscordUnreachableException : Exception
{
    public DiscordUnreachableException()
        : base("Discord could not be reached.")
    {
    }
}
