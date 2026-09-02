using System.Diagnostics;
using Discord;
using Discord.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ExplorationTests.DiscordCanary;

/// <summary>
///     Exercises IBotClient.Restart against the real gateway: the replacement client must come
///     up Connected and still be able to post. Nothing below the exploration workbench touches
///     a real gateway, so this is the only place the swap itself is proven
///     (docs/design/discord-overhaul.md §10). Leaves one line in the lab channel per run.
/// </summary>
[Collection(DiscordCanaryCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class GatewayRestartCanaryTests
{
    [DiscordCanaryFact]
    public async Task RestartBringsUpAReplacementClientThatStillPosts()
    {
        var marker = $"gateway restart canary {Guid.NewGuid():N}";
        using var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration
            {
                BotToken = DiscordCanaryTests.CanaryToken!, RichScoreMessages = true
            }));
        Assert.Equal(BotGatewayStatus.NotStarted, bot.Status);

        await bot.Start();
        await WaitForConnected(bot);

        await bot.Restart();

        // The replacement identifies afresh; the downtime clock restarted with it.
        await WaitForConnected(bot);
        await bot.SendMessages(new[] { marker }, new[] { DiscordCanaryTests.CanaryChannel!.Value });

        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, DiscordCanaryTests.CanaryToken);
        var channel = Assert.IsType<IMessageChannel>(
            await rest.GetChannelAsync(DiscordCanaryTests.CanaryChannel.Value), exactMatch: false);
        var recent = (await channel.GetMessagesAsync(10).FlattenAsync()).ToArray();

        Assert.Contains(recent, m => m.Content.Contains(marker));
        await bot.Stop();
    }

    private static async Task WaitForConnected(DiscordBotClient bot)
    {
        var waited = Stopwatch.StartNew();
        while (bot.Status.State != BotGatewayState.Connected)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30),
                $"gateway not Connected within 30 s; last status {bot.Status}");
            await Task.Delay(250);
        }
    }
}
