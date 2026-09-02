using Discord;
using Microsoft.Extensions.Logging;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Turns a Discord.Net log entry into what the app's logger needs. Discord.Net reports a
///     dropped gateway connection as an exception-only entry (null message), so the text falls
///     back to the exception's message and the exception itself travels with the log line.
///     Forwarding only <see cref="LogMessage.Message" /> is how a fourteen-hour reconnect loop
///     once logged as nothing but "[null]" (docs/design/discord-overhaul.md §10).
/// </summary>
public static class DiscordLogMapping
{
    public static LogLevel ToLogLevel(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
    }

    /// <summary>The line's text: the message when there is one, else the exception's.</summary>
    public static string Text(LogMessage message)
    {
        // An empty message is as blank as a null one; the exception's text is what the hook exists to keep.
        return string.IsNullOrEmpty(message.Message)
            ? message.Exception?.Message ?? string.Empty
            : message.Message;
    }
}
