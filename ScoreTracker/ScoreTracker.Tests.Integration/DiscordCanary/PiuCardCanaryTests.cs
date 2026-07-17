using Discord;
using Discord.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Tests.Integration.DiscordCanary;

/// <summary>
///     Posts the /piu command-reply and feed sample cards to the owner's lab channel and
///     reads them back over REST — the same posture as <see cref="DiscordCanaryTests" />,
///     scoped to the Discord overhaul's new surfaces. Each card mirrors what the
///     BotCommandSaga / DiscordFeedSaga compose, so the lab channel doubles as a live
///     gallery of the real shapes. Manual runs only.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PiuCardCanaryTests
{
    private static readonly Uri SongArt = new("https://piuimages.arroweclip.se/songs/WitchDoctor.png");
    private const string ChartBase = "https://piuscores.arroweclip.se/Chart";

    [DiscordCanaryFact]
    public Task PostsTheChartLookupCard() => PostAndVerify(ChartCard);

    [DiscordCanaryFact]
    public Task PostsTheRandomDrawCard() => PostAndVerify(RandomCard);

    [DiscordCanaryFact]
    public Task PostsTheSuggestCard() => PostAndVerify(SuggestCard);

    // /piu chart — song art header, one difficulty-bubble row per chart, each a masked link.
    private static RichBotMessage ChartCard(string marker) =>
        new(new RichBotSection("### Witch Doctor\n-# BanYa · 175 BPM · Phoenix 2", SongArt),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText(
                    $"#DIFFICULTY|S18# [S18]({ChartBase}/00000000-0000-0000-0000-000000000001)\n" +
                    $"#DIFFICULTY|D19# [D19]({ChartBase}/00000000-0000-0000-0000-000000000002)\n" +
                    $"#DIFFICULTY|S22# [S22]({ChartBase}/00000000-0000-0000-0000-000000000003)")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            Array.Empty<RichBotLink>());

    // /piu random — a titled draw, one art row per chart, each a masked link.
    private static RichBotMessage RandomCard(string marker) =>
        new(new RichBotSection("### Drew 3 charts\n-# Doubles · levels 20–23 · Phoenix 2", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection($"#DIFFICULTY|D22# [Sarabande]({ChartBase}/00000000-0000-0000-0000-000000000021)",
                    SongArt),
                new RichBotSection($"#DIFFICULTY|D20# [Moonlight]({ChartBase}/00000000-0000-0000-0000-000000000022)",
                    SongArt),
                new RichBotSection($"#DIFFICULTY|D23# [Gargoyle FS]({ChartBase}/00000000-0000-0000-0000-000000000023)",
                    SongArt)
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            Array.Empty<RichBotLink>());

    // /piu suggest — ephemeral in practice; each row carries the engine's explanation.
    private static RichBotMessage SuggestCard(string marker) =>
        new(new RichBotSection("### Suggested for you — Title Hunt\n-# Phoenix 2 · based on your scores", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S21# [District 1]({ChartBase}/00000000-0000-0000-0000-0000000000a1)\n" +
                    "-# [Expert Lv.7] — needs 985,000+, you're at 981,220", SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|D19# [Vacuum]({ChartBase}/00000000-0000-0000-0000-0000000000a2)\n" +
                    "-# [VACUUM Lv.3] — skill title progress, 942k/990k", SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|S20# [1949]({ChartBase}/00000000-0000-0000-0000-0000000000a3)\n" +
                    "-# unplayed in this folder", SongArt)
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("Open Suggested Charts", new Uri("https://piuscores.arroweclip.se")) });

    // Starts the real bot, posts the marked card(s), and asserts each landed with V2
    // components attached (independent REST readback, not just "the send didn't throw").
    private static async Task PostAndVerify(params Func<string, RichBotMessage>[] cardFactories)
    {
        var token = DiscordCanaryTests.CanaryToken!;
        var channelId = DiscordCanaryTests.CanaryChannel!.Value;
        var marker = $"canary {Guid.NewGuid():N}";
        var cards = cardFactories.Select(f => f(marker)).ToArray();

        using var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration { BotToken = token, RichScoreMessages = true }));
        await bot.Start();
        var ready = new TaskCompletionSource();
        bot.WhenReady(() =>
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await bot.SendRichMessages(cards, new[] { channelId });

        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, token);
        var channel = Assert.IsType<IMessageChannel>(await rest.GetChannelAsync(channelId), exactMatch: false);
        var recent = (await channel.GetMessagesAsync(20).FlattenAsync()).ToArray();
        var mine = recent.Where(m => DiscordCanaryTests.ComponentTexts(m.Components).Any(t => t.Contains(marker)))
            .ToArray();

        Assert.Equal(cards.Length, mine.Length);
        await bot.Stop();
    }
}
