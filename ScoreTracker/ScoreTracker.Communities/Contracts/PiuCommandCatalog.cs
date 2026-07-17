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
                new[] { Calc },
                Array.Empty<BotSubCommandGroup>())
        };

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
