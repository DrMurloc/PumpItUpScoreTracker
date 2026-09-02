namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Hangfire trigger, every two minutes: ask the gateway watchdog whether the bot's socket
///     has been down long enough to replace the client (docs/design/discord-overhaul.md §10).
///     Lives in Communities because it already owns every Discord composition path and takes
///     the bot port; nothing new crosses a vertical boundary for it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CheckDiscordGatewayCommand;
