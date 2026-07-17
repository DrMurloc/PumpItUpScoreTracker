using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts
{
    /// <summary>
    ///     The single definition of the <c>/piu</c> command tree the bot registers. The
    ///     Communities router (BotCommandSaga) matches on the same subcommand names, so the
    ///     surface and its handling stay in lockstep.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class PiuCommandCatalog
    {
        public const string RootName = "piu";

        public static IReadOnlyList<BotCommandDefinition> Commands { get; } = new[]
        {
            new BotCommandDefinition(RootName, "PIU Scores tools",
                new[] { Calc, Chart, Unregister, Feeds },
                new[] { Register })
        };

        private static readonly IReadOnlyList<BotOptionChoice> MixChoices = new[]
        {
            new BotOptionChoice("Phoenix", "Phoenix"),
            new BotOptionChoice("Phoenix 2", "Phoenix2")
        };

        private static BotCommandOption RequiredMix =>
            new("mix", "Which mix", BotCommandOptionType.String, Required: true, Choices: MixChoices);

        private static BotCommandOption OptionalMix =>
            new("mix", "Which mix (defaults to Phoenix 2)", BotCommandOptionType.String, Choices: MixChoices);

        private static BotSubCommand Chart =>
            new("chart", "Find a chart and its pages",
                new[]
                {
                    new BotCommandOption("song", "Song name", BotCommandOptionType.String, Required: true,
                        Autocomplete: true),
                    OptionalMix
                });

        private static BotSubCommandGroup Register =>
            new("register", "Register this channel for a feed",
                new[]
                {
                    new BotSubCommand("community", "Community score and title notifications",
                        new[]
                        {
                            new BotCommandOption("name", "Community name", BotCommandOptionType.String,
                                Autocomplete: true),
                            new BotCommandOption("invite-code", "Invite code (private communities only)")
                        }, Ephemeral: true),
                    new BotSubCommand("weekly", "Weekly Charts — results and the new lineup",
                        new[] { RequiredMix }, Ephemeral: true),
                    new BotSubCommand("daily", "Daily Step — yesterday's board and today's chart",
                        new[] { RequiredMix }, Ephemeral: true),
                    new BotSubCommand("official", "Official leaderboards — the weekly digest",
                        new[] { RequiredMix }, Ephemeral: true)
                });

        private static BotSubCommand Unregister =>
            new("unregister", "Stop one of this channel's feeds",
                new[]
                {
                    new BotCommandOption("feed", "Which registration to remove", BotCommandOptionType.String,
                        Required: true, Autocomplete: true)
                }, Ephemeral: true);

        private static BotSubCommand Feeds =>
            new("feeds", "Show what this channel is registered for",
                Array.Empty<BotCommandOption>(), Ephemeral: true);

        private static BotSubCommand Calc =>
            new("calc", "Calculate a Phoenix score from a result screen",
                new[]
                {
                    Count("perfects", "Perfect count"),
                    Count("greats", "Great count"),
                    Count("goods", "Good count"),
                    Count("bads", "Bad count"),
                    Count("misses", "Miss count"),
                    Count("combo", "Max combo"),
                    new BotCommandOption("calories", "Calories burned (optional — estimates step count)",
                        BotCommandOptionType.Integer, MinValue: 0)
                });

        private static BotCommandOption Count(string name, string description) =>
            new(name, description, BotCommandOptionType.Integer, Required: true, MinValue: 0);
    }
}
