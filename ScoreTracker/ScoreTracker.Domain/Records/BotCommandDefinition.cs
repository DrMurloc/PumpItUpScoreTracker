namespace ScoreTracker.Domain.Records;

/// <summary>The provider-agnostic option kinds a bot command exposes.</summary>
public enum BotCommandOptionType
{
    String,
    Integer,
    Boolean
}

/// <summary>
///     A fixed choice offered for an option (the value is always carried as a string).
///     <paramref name="NameLocalizations" /> maps supported culture codes to the display
///     name each viewer's client shows; the adapter converts codes to provider locales.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotOptionChoice(string Name, string Value,
    IReadOnlyDictionary<string, string>? NameLocalizations = null);

/// <summary>
///     One option on a subcommand. <paramref name="Autocomplete" /> and fixed
///     <paramref name="Choices" /> are mutually exclusive at the Discord layer — a choice
///     list is a closed set, autocomplete is an open live lookup.
///     <paramref name="DescriptionLocalizations" /> maps supported culture codes to
///     per-client help text.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotCommandOption(
    string Name,
    string Description,
    BotCommandOptionType Type = BotCommandOptionType.String,
    bool Required = false,
    bool Autocomplete = false,
    IReadOnlyList<BotOptionChoice>? Choices = null,
    long? MinValue = null,
    long? MaxValue = null,
    IReadOnlyDictionary<string, string>? DescriptionLocalizations = null);

/// <summary>
///     A leaf command the user actually invokes. <paramref name="Ephemeral" /> is fixed at
///     definition time because the reply visibility is chosen when the interaction is
///     deferred, before the handler runs.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotSubCommand(
    string Name,
    string Description,
    IReadOnlyList<BotCommandOption> Options,
    bool Ephemeral = false,
    IReadOnlyDictionary<string, string>? DescriptionLocalizations = null);

/// <summary>A group of related subcommands (e.g. <c>register</c> → community/weekly/daily/official).</summary>
[ExcludeFromCodeCoverage]
public sealed record BotSubCommandGroup(
    string Name,
    string Description,
    IReadOnlyList<BotSubCommand> SubCommands,
    IReadOnlyDictionary<string, string>? DescriptionLocalizations = null);

/// <summary>
///     A top-level bot command tree. Directly-invokable subcommands and grouped
///     subcommands both hang off the one command (e.g. <c>/piu</c>). The adapter maps this
///     to the provider's command surface and registers it atomically.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BotCommandDefinition(
    string Name,
    string Description,
    IReadOnlyList<BotSubCommand> SubCommands,
    IReadOnlyList<BotSubCommandGroup> SubCommandGroups,
    IReadOnlyDictionary<string, string>? DescriptionLocalizations = null);
