namespace ScoreTracker.ExplorationTests.DiscordCanary;

/// <summary>
///     Marks the real-session showcase: a manual-run-only harness that replays a real
///     play session out of a LOCAL development database through the production card
///     pipeline (in-memory bus → CommunitySaga → Components V2 renderer → the TESTING
///     bot) into the owner's lab channel. On top of the canary's config it needs the
///     local database's connection string — never configured in CI, so it always skips
///     there. It mutates that database (journal/highlight/milestone rows for the demo
///     session), which is sanctioned for the local dev database only.
/// </summary>
public sealed class SessionShowcaseFactAttribute : FactAttribute
{
    public SessionShowcaseFactAttribute()
    {
        if (!RealSessionShowcaseTests.Configured)
            Skip = "Session showcase: configure Discord:BotToken + DiscordTest:CanaryChannelId + " +
                   "DiscordTest:ExampleConnectionString user-secrets (AppHost store) to replay a real " +
                   "session from the local database to the lab channel.";
    }
}
