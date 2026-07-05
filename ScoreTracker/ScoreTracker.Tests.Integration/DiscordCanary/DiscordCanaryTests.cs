using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Tests.Integration.DiscordCanary;

/// <summary>
///     Posts the sample score cards to the owner's lab channel and reads them back over
///     REST. What it buys: Discord API contract drift (V2 payload rejections), emoji-id
///     resolution, and token/permission validity — the failure modes component tests
///     can't see. Messages are deliberately left in the channel: it doubles as a
///     human-glanceable gallery of what the cards looked like on every run.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DiscordCanaryTests
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<DiscordCanaryTests>(optional: true)
            .Build());

    private static string? Token =>
        Environment.GetEnvironmentVariable("DISCORD_CANARY_TOKEN") ?? Configuration.Value["Discord:BotToken"];

    private static ulong? ChannelId =>
        ulong.TryParse(
            Environment.GetEnvironmentVariable("DISCORD_CANARY_CHANNEL") ??
            Configuration.Value["DiscordTest:CanaryChannelId"], out var id)
            ? id
            : null;

    public static bool Configured => !string.IsNullOrWhiteSpace(Token) && ChannelId != null;

    [DiscordCanaryFact]
    public async Task PostsSampleCardsToTheLabChannelAndReadsThemBack()
    {
        var marker = $"canary {Guid.NewGuid():N}";
        using var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration { BotToken = Token!, RichScoreMessages = true }));
        await bot.Start();
        var ready = new TaskCompletionSource();
        bot.WhenReady(() =>
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await bot.SendRichMessages(new[] { SamplePassesCard(marker), SampleDigestCard(marker) },
            new[] { ChannelId!.Value });

        // Independent REST readback — proves the messages actually landed with V2
        // components attached, not just that the socket client didn't throw.
        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, Token);
        var channel = Assert.IsAssignableFrom<IMessageChannel>(await rest.GetChannelAsync(ChannelId.Value));
        var recent = (await channel.GetMessagesAsync(15).FlattenAsync()).ToArray();
        var mine = recent.Where(m => ComponentTexts(m.Components).Any(t => t.Contains(marker))).ToArray();

        Assert.Equal(2, mine.Length);
        await bot.Stop();
    }

    private static RichBotMessage SamplePassesCard(string marker)
    {
        var avatar = new Uri("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");
        return new RichBotMessage(
            new RichBotSection("### **Canary** passed 2 charts", avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection(
                    "#DIFFICULTY|d23# 👑 Sample Song\n**970,207** #LETTERGRADE|S|False##PLATE|UltimateGame#",
                    avatar),
                new RichBotSection(
                    "#DIFFICULTY|s17# 📊 Another Song\n**999,150** #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#",
                    avatar),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|d23# 84/141 (59.6%) · #DIFFICULTY|s17# 182/195 (93.3%)")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            0xE8C24A,
            new[] { new RichBotLink("View all recent scores", new Uri("https://piuscores.arroweclip.se")) });
    }

    private static RichBotMessage SampleDigestCard(string marker)
    {
        var avatar = new Uri("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");
        return new RichBotMessage(
            new RichBotSection("### [Phoenix 2] **Canary** passed 1,872 · upscored 141", avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection(
                    "#DIFFICULTY|d23# 👑 Digest Highlight\n**962,410** #LETTERGRADE|AAAPlus|False##PLATE|FairGame#",
                    avatar),
                new RichBotText("…and 36 more highlights"),
                new RichBotDivider(),
                new RichBotText("Levels S1–S22 · D4–D23 — highest new pass D23")
            },
            $"#MIX|Phoenix2# Phoenix2 · PIU Scores · {marker}",
            0xE8C24A,
            Array.Empty<RichBotLink>());
    }

    private static IEnumerable<string> ComponentTexts(IEnumerable<IMessageComponent> components)
    {
        foreach (var component in components)
            switch (component)
            {
                case TextDisplayComponent text:
                    yield return text.Content;
                    break;
                case ContainerComponent container:
                    foreach (var inner in ComponentTexts(container.Components)) yield return inner;
                    break;
                case SectionComponent section:
                    foreach (var inner in ComponentTexts(section.Components)) yield return inner;
                    break;
            }
    }
}
