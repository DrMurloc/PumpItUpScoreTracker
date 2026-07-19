namespace ScoreTracker.Domain.Records;

/// <summary>
///     A resolved slash-command invocation handed to the command handler. The
///     <see cref="CommandPath" /> is the subcommand path below the top-level command (e.g.
///     <c>["register","weekly"]</c> or <c>["chart"]</c>); <see cref="Options" /> holds the
///     leaf option values as strings, keyed by option name.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotInteraction(
    IReadOnlyList<string> CommandPath,
    IReadOnlyDictionary<string, string> Options,
    ulong ChannelId,
    ulong? GuildId,
    ulong UserId,
    string UserDisplayName,
    bool InvokerCanManageChannels);

/// <summary>
///     A live autocomplete request for the focused option. Handlers typically switch on
///     <see cref="FocusedOptionName" /> (option names are unique across the tree) and may
///     read already-entered sibling values from <see cref="Options" />.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotAutocompleteRequest(
    IReadOnlyList<string> CommandPath,
    string FocusedOptionName,
    string PartialValue,
    IReadOnlyDictionary<string, string> Options,
    ulong UserId,
    ulong ChannelId,
    ulong? GuildId);

/// <summary>
///     A command handler's response: either a rich <see cref="Card" /> or plain
///     <see cref="Text" /> (card wins when both are set). Reply visibility is not carried
///     here — it is fixed by the subcommand's <see cref="BotSubCommand.Ephemeral" /> flag,
///     applied when the interaction was deferred.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotReply(RichBotMessage? Card = null, string? Text = null);
