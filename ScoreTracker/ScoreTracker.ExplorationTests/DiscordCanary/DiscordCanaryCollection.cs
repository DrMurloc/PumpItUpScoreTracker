namespace ScoreTracker.ExplorationTests.DiscordCanary;

/// <summary>
///     The lab-channel canaries run one class at a time. Every one of them logs the same bot
///     in, and Discord admits one IDENTIFY per five seconds per bot, so five classes starting at
///     once queue behind each other and the thirty-second waits for Ready expire on whichever
///     came last. Alone, each passes in seconds.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DiscordCanaryCollection
{
    public const string Name = "Discord canary";
}
