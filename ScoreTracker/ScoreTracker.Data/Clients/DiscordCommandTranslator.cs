using Discord;
using Discord.WebSocket;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Pure translation between the provider-agnostic <see cref="BotCommandDefinition" />
///     tree and Discord's command surface — building the registerable command properties,
///     and resolving an incoming interaction/autocomplete back into a
///     (<c>path</c>, <c>options</c>) pair. Kept free of the socket client so the mapping is
///     unit-testable without a live gateway.
/// </summary>
public static class DiscordCommandTranslator
{
    public static SlashCommandProperties ToProperties(BotCommandDefinition definition)
    {
        var builder = new SlashCommandBuilder()
            .WithName(definition.Name)
            .WithDescription(definition.Description);
        foreach (var sub in definition.SubCommands)
            builder.AddOption(BuildSubCommand(sub));
        foreach (var group in definition.SubCommandGroups)
            builder.AddOption(BuildGroup(group));
        return builder.Build();
    }

    /// <summary>True if the subcommand at <paramref name="path" /> is declared ephemeral.</summary>
    public static bool IsEphemeral(BotCommandDefinition definition, IReadOnlyList<string> path)
    {
        var sub = ResolveSubCommand(definition, path);
        return sub?.Ephemeral ?? false;
    }

    public static BotSubCommand? ResolveSubCommand(BotCommandDefinition definition, IReadOnlyList<string> path)
    {
        switch (path.Count)
        {
            case 1:
                return definition.SubCommands.FirstOrDefault(s => s.Name == path[0]);
            case 2:
                return definition.SubCommandGroups.FirstOrDefault(g => g.Name == path[0])
                    ?.SubCommands.FirstOrDefault(s => s.Name == path[1]);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Walks a received slash command's option tree, peeling subcommand-group and
    ///     subcommand wrappers into the path and collecting the leaf option values.
    /// </summary>
    public static (IReadOnlyList<string> Path, IReadOnlyDictionary<string, string> Options) ResolveInvocation(
        SocketSlashCommand command)
    {
        var path = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyCollection<SocketSlashCommandDataOption> current = command.Data.Options;
        while (true)
        {
            var first = current.FirstOrDefault();
            if (first == null) break;
            if (first.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup)
            {
                path.Add(first.Name);
                current = first.Options ?? new List<SocketSlashCommandDataOption>();
            }
            else
            {
                foreach (var leaf in current)
                    options[leaf.Name] = leaf.Value?.ToString() ?? string.Empty;
                break;
            }
        }

        return (path, options);
    }

    /// <summary>
    ///     Resolves an autocomplete interaction into the subcommand path plus the sibling
    ///     option values already entered (the focused option itself is read from the
    ///     interaction's Current).
    /// </summary>
    public static (IReadOnlyList<string> Path, IReadOnlyDictionary<string, string> Options) ResolveAutocomplete(
        SocketAutocompleteInteraction interaction)
    {
        // Autocomplete options arrive flat (no nested children): subcommand-typed entries
        // give the path, the remaining non-focused entries are the sibling values already
        // filled in. The focused option itself is read from the interaction's Current.
        var path = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in interaction.Data.Options)
            if (option.Type is ApplicationCommandOptionType.SubCommand
                or ApplicationCommandOptionType.SubCommandGroup)
                path.Add(option.Name);
            else if (!option.Focused)
                options[option.Name] = option.Value?.ToString() ?? string.Empty;

        return (path, options);
    }

    private static SlashCommandOptionBuilder BuildGroup(BotSubCommandGroup group)
    {
        var builder = new SlashCommandOptionBuilder()
            .WithName(group.Name)
            .WithDescription(group.Description)
            .WithType(ApplicationCommandOptionType.SubCommandGroup);
        foreach (var sub in group.SubCommands)
            builder.AddOption(BuildSubCommand(sub));
        return builder;
    }

    private static SlashCommandOptionBuilder BuildSubCommand(BotSubCommand sub)
    {
        var builder = new SlashCommandOptionBuilder()
            .WithName(sub.Name)
            .WithDescription(sub.Description)
            .WithType(ApplicationCommandOptionType.SubCommand);
        foreach (var option in sub.Options)
            builder.AddOption(BuildOption(option));
        return builder;
    }

    private static SlashCommandOptionBuilder BuildOption(BotCommandOption option)
    {
        var builder = new SlashCommandOptionBuilder()
            .WithName(option.Name)
            .WithDescription(option.Description)
            .WithType(MapType(option.Type))
            .WithRequired(option.Required);
        if (option.Autocomplete) builder.WithAutocomplete(true);
        if (option.MinValue != null) builder.WithMinValue(option.MinValue.Value);
        if (option.MaxValue != null) builder.WithMaxValue(option.MaxValue.Value);
        if (option.Choices != null)
            foreach (var choice in option.Choices)
                builder.AddChoice(choice.Name, choice.Value);
        return builder;
    }

    private static ApplicationCommandOptionType MapType(BotCommandOptionType type) => type switch
    {
        BotCommandOptionType.Integer => ApplicationCommandOptionType.Integer,
        BotCommandOptionType.Boolean => ApplicationCommandOptionType.Boolean,
        _ => ApplicationCommandOptionType.String
    };
}
