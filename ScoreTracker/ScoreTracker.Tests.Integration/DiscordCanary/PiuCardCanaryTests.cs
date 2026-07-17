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

    [DiscordCanaryFact]
    public Task PostsTheWeeklyFeedCards() => PostAndVerify(WeeklyResultCard, WeeklyLineupCard);

    [DiscordCanaryFact]
    public Task PostsTheDailyFeedCard() => PostAndVerify(DailyFeedCard);

    // Weekly feed — one result card per most-played chart (green rows = community members).
    private static RichBotMessage WeeklyResultCard(string marker) =>
        new(new RichBotSection(
                "### Weekly Charts — final board\n-# [Phoenix 2] District 1 #DIFFICULTY|D24# · 18 players · week of Jul 7",
                SongArt),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText(
                    "` 1` **JEWEL** — 997,821 #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#\n" +
                    "` 2` 🟢 **ESI** — 993,204 #LETTERGRADE|SSS|False##PLATE|ExtremeGame#\n" +
                    "` 3` **PUMPKING** — 988,917 #LETTERGRADE|SSPlus|False##PLATE|ExtremeGame#\n" +
                    "` 4` 🟢 **WABBIT** — 983,660 #LETTERGRADE|SS|False##PLATE|SuperbGame#\n" +
                    "` 5` **NIMGO** — 982,105 #LETTERGRADE|SS|False##PLATE|SuperbGame#")
            },
            $"#MIX|Phoenix2# Card 1 of 5 · 12 more charts had entries · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            Array.Empty<RichBotLink>());

    private static RichBotMessage WeeklyLineupCard(string marker) =>
        new(new RichBotSection("### This week's charts\n-# [Phoenix 2] one per level bucket", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText(
                    $"#DIFFICULTY|S16# [Trashy Innocence]({ChartBase}/00000000-0000-0000-0000-0000000000b1) · " +
                    $"#DIFFICULTY|S20# [1949]({ChartBase}/00000000-0000-0000-0000-0000000000b2) · " +
                    $"#DIFFICULTY|D23# [Sarabande]({ChartBase}/00000000-0000-0000-0000-0000000000b3)")
            },
            $"#MIX|Phoenix2# Phoenix 2 · resets Monday midnight ET · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("Full results", new Uri("https://piuscores.arroweclip.se/WeeklyCharts")),
                new RichBotLink("Weekly Charts", new Uri("https://piuscores.arroweclip.se/WeeklyCharts"))
            });

    // Daily feed — yesterday's board (Limbo mocked) + today's chart in one card.
    private static RichBotMessage DailyFeedCard(string marker) =>
        new(new RichBotSection("### Daily Step\n-# [Phoenix 2] yesterday's board settled", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("**Yesterday — Trashy Innocence #DIFFICULTY|S19#**"),
                new RichBotText(
                    "` 1` **ESI** — 996,410 #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#\n" +
                    "` 2` 🟢 **MELON** — 991,077 #LETTERGRADE|SSS|False##PLATE|ExtremeGame#\n" +
                    "` 3` **TUSA** — 987,215 #LETTERGRADE|SSPlus|False##PLATE|SuperbGame#"),
                new RichBotDivider(),
                new RichBotSection(
                    $"**Today — [Bee]({ChartBase}/00000000-0000-0000-0000-0000000000c1) #DIFFICULTY|S7#**\n" +
                    "-# 🕯 **Limbo Day** — lowest passing score wins. No breaking.", SongArt)
            },
            $"#MIX|Phoenix2# Phoenix 2 · resets midnight ET · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("Daily Step board", new Uri("https://piuscores.arroweclip.se")) });

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
