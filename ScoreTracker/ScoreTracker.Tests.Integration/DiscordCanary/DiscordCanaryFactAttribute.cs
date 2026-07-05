namespace ScoreTracker.Tests.Integration.DiscordCanary;

/// <summary>
///     Marks a test that talks to real Discord with the owner's TESTING bot. Manual runs
///     only — no schedule, never the PR gate (owner call: breakage gets heard from the
///     communities faster than a scheduled run would report it). Run it when a change
///     touches Discord or Communities code. Configuration comes from the shared AppHost
///     user-secrets store (Discord:BotToken + DiscordTest:CanaryChannelId) or the
///     DISCORD_CANARY_TOKEN / DISCORD_CANARY_CHANNEL environment variables; unconfigured
///     environments (CI included) skip.
/// </summary>
public sealed class DiscordCanaryFactAttribute : FactAttribute
{
    public DiscordCanaryFactAttribute()
    {
        if (!DiscordCanaryTests.Configured)
            Skip = "Discord canary: configure Discord:BotToken + DiscordTest:CanaryChannelId user-secrets " +
                   "(AppHost store) or DISCORD_CANARY_TOKEN/DISCORD_CANARY_CHANNEL env vars to post to the lab channel.";
    }
}
