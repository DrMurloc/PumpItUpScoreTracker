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
                new[] { Calc, Chart, Random, Suggest, Unregister, Feeds },
                new[] { Register })
        };

        private static readonly IReadOnlyList<BotOptionChoice> GoalChoices = new[]
        {
            new BotOptionChoice("Title Hunt", "TitleHunt"),
            new BotOptionChoice("Score Push", "ScorePush"),
            new BotOptionChoice("Fill Gaps", "FillGaps"),
            new BotOptionChoice("Pumbility Push", "PumbilityPush")
        };

        private static readonly IReadOnlyList<BotOptionChoice> SinglesDoublesChoices = new[]
        {
            new BotOptionChoice("Singles", "Single"),
            new BotOptionChoice("Doubles", "Double")
        };

        private static BotSubCommand Suggest =>
            new("suggest", "Personalized chart suggestions (private)",
                new[]
                {
                    new BotCommandOption("goal", "What to work on (default Title Hunt)", BotCommandOptionType.String,
                        Choices: GoalChoices),
                    new BotCommandOption("type", "Singles or Doubles", BotCommandOptionType.String,
                        Choices: SinglesDoublesChoices),
                    OptionalMix
                }, Ephemeral: true);

        private static readonly IReadOnlyList<BotOptionChoice> MixChoices = new[]
        {
            new BotOptionChoice("Phoenix", "Phoenix"),
            new BotOptionChoice("Phoenix 2", "Phoenix2")
        };

        private static readonly IReadOnlyList<BotOptionChoice> TypeChoices = new[]
        {
            new BotOptionChoice("Singles", "Single"),
            new BotOptionChoice("Doubles", "Double"),
            new BotOptionChoice("Co-op", "CoOp")
        };

        private static BotSubCommand Random =>
            new("random", "Draw a random set of charts",
                new[]
                {
                    new BotCommandOption("count", "How many (1–10, default 3)", BotCommandOptionType.Integer,
                        MinValue: 1, MaxValue: 10),
                    new BotCommandOption("type", "Chart type (default Singles)", BotCommandOptionType.String,
                        Choices: TypeChoices),
                    new BotCommandOption("min-level", "Lowest level", BotCommandOptionType.Integer,
                        MinValue: 1, MaxValue: 29),
                    new BotCommandOption("max-level", "Highest level", BotCommandOptionType.Integer,
                        MinValue: 1, MaxValue: 29),
                    OptionalMix,
                    new BotCommandOption("preset", "One of your saved randomizer presets", BotCommandOptionType.String,
                        Autocomplete: true)
                });

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

        // Native names, universal in every viewer's client; the value is the stored culture code.
        private static readonly IReadOnlyList<BotOptionChoice> LanguageChoices = SupportedCultures.All
            .Select(c => new BotOptionChoice(c.NativeName, c.Code))
            .ToArray();

        private static BotCommandOption Language =>
            new("language", "Language for this channel's posts (default English)", BotCommandOptionType.String,
                Choices: LanguageChoices);

        private static BotSubCommandGroup Register =>
            new("register", "Register this channel for a feed",
                new[]
                {
                    new BotSubCommand("community", "Community score and title notifications",
                        new[]
                        {
                            new BotCommandOption("name", "Community name", BotCommandOptionType.String,
                                Autocomplete: true),
                            new BotCommandOption("invite-code", "Invite code (private communities only)"),
                            Language
                        }, Ephemeral: true),
                    new BotSubCommand("weekly", "Weekly Charts — results and the new lineup",
                        new[] { RequiredMix, Language }, Ephemeral: true),
                    new BotSubCommand("daily", "Daily Step — yesterday's board and today's chart",
                        new[] { RequiredMix, Language }, Ephemeral: true),
                    new BotSubCommand("official", "Official leaderboards — the weekly digest",
                        new[] { RequiredMix, Language }, Ephemeral: true)
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
