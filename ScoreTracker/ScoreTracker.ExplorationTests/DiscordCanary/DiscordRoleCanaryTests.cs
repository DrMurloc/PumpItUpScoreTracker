using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ExplorationTests.DiscordCanary;

/// <summary>
///     Marks a test that grants and revokes a real Discord role. Manual only, and gated on THREE
///     extra secrets rather than the canary's two, so an environment configured for the posting
///     canary does not start writing roles by accident.
/// </summary>
public sealed class DiscordRoleCanaryFactAttribute : FactAttribute
{
    public DiscordRoleCanaryFactAttribute()
    {
        if (!DiscordRoleCanaryTests.Configured)
            Skip = "Discord role canary: set DiscordTest:CanaryGuildId, DiscordTest:CanaryRoleId and " +
                   "DiscordTest:CanaryUserId (AppHost user-secrets) alongside Discord:BotToken. " +
                   "The role must be one the bot may assign and nobody relies on.";
    }
}

/// <summary>
///     The only test in the solution that WRITES to Discord beyond posting a message: it grants a
///     throwaway role to one account in the owner's lab server, reads it back, and takes it off
///     again. Owner-authorized 2026-09-09 for exactly this, against a role created for it.
///     <para>
///         What it buys is the half no mocked suite can reach: that the bot's token actually
///         carries Manage Roles, that the role sits below the bot in the hierarchy, and that a
///         single member can be fetched by id with the members intent enabled — the three ways
///         this feature fails silently in production.
///     </para>
///     <para>
///         It touches one role and one account, both named in user-secrets, and it restores the
///         starting state whether or not the assertions pass. It is inert without those secrets,
///         so CI and every other machine skip it.
///     </para>
/// </summary>
[Collection(DiscordCanaryCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class DiscordRoleCanaryTests
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<DiscordRoleCanaryTests>(optional: true)
            .Build());

    private static string? Token =>
        Environment.GetEnvironmentVariable("DISCORD_CANARY_TOKEN") ?? Configuration.Value["Discord:BotToken"];

    private static ulong? GuildId => Snowflake("DiscordTest:CanaryGuildId", "DISCORD_CANARY_GUILD");
    private static ulong? RoleId => Snowflake("DiscordTest:CanaryRoleId", "DISCORD_CANARY_ROLE");
    private static ulong? UserId => Snowflake("DiscordTest:CanaryUserId", "DISCORD_CANARY_USER");

    public static bool Configured =>
        !string.IsNullOrWhiteSpace(Token) && GuildId != null && RoleId != null && UserId != null;

    private static ulong? Snowflake(string key, string variable) =>
        ulong.TryParse(Environment.GetEnvironmentVariable(variable) ?? Configuration.Value[key], out var id)
            ? id
            : null;

    [DiscordRoleCanaryFact]
    public async Task GrantsAndRevokesARoleAgainstRealDiscord()
    {
        using var bot = await Connect();
        try
        {
            var guild = await bot.GetGuild(GuildId!.Value);
            Assert.NotNull(guild);

            // The half a mocked suite cannot reach, and the one this canary found on its first
            // run: the bot was invited with the old permission integer and holds no Manage Roles
            // at all, so every position check passes and the write returns 50001 Missing Access.
            Assert.True(guild!.CanManageRoles,
                "The bot has no Manage Roles permission in this server. Re-run the invite URL from " +
                "/Communities — moving roles around cannot fix this one.");

            var roles = await bot.GetGuildRoles(GuildId.Value);
            var role = Assert.Single(roles, r => r.Id == RoleId!.Value);

            // The hierarchy check. A role above the bot is refused silently, so a canary that
            // skipped this would pass while production handed out nothing at all.
            Assert.True(role.CanAssign,
                $"The canary role is not assignable ({role.BlockedReason}). Move the bot's role above it.");

            var before = await bot.GetMemberRoles(GuildId.Value, UserId!.Value);
            Assert.NotNull(before);
            Assert.DoesNotContain(RoleId.Value, before!);

            await bot.AddRole(GuildId.Value, UserId.Value, RoleId.Value);
            var granted = await bot.GetMemberRoles(GuildId.Value, UserId.Value);
            Assert.Contains(RoleId.Value, granted!);

            await bot.RemoveRole(GuildId.Value, UserId.Value, RoleId.Value);
            var revoked = await bot.GetMemberRoles(GuildId.Value, UserId.Value);
            Assert.DoesNotContain(RoleId.Value, revoked!);
        }
        finally
        {
            // A failed assertion mid-run must not leave the role on. Best-effort: the bot may be
            // the reason the run failed in the first place.
            try
            {
                await bot.RemoveRole(GuildId!.Value, UserId!.Value, RoleId!.Value);
            }
            catch (Exception)
            {
                // Nothing useful to do here; the role is named in secrets and removable by hand.
            }

            await bot.Stop();
        }
    }

    /// <summary>
    ///     Somebody who is not in the server reads back as null rather than as an empty set — the
    ///     distinction the reconcile relies on to tell "holds nothing" from "not here".
    /// </summary>
    [DiscordRoleCanaryFact]
    public async Task AStrangerToTheServerReadsBackAsNull()
    {
        using var bot = await Connect();
        try
        {
            // A syntactically valid snowflake that is nobody: Discord's own epoch start.
            Assert.Null(await bot.GetMemberRoles(GuildId!.Value, 1UL));
        }
        finally
        {
            await bot.Stop();
        }
    }

    private static async Task<DiscordBotClient> Connect()
    {
        var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration { BotToken = Token!, RichScoreMessages = true }));
        await bot.Start();
        var ready = new TaskCompletionSource();
        bot.WhenReady(() =>
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return bot;
    }
}
