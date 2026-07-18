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
    private const string Vid = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

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

    [DiscordCanaryFact]
    public Task PostsTheOfficialDigestCard() => PostAndVerify(OfficialDigestCard);

    // Official digest — opens with the top 10 + rank movement, then movers/firsts, and
    // "what it takes" framed as the two difficulty levels.
    private static RichBotMessage OfficialDigestCard(string marker) =>
        new(new RichBotSection("### This week on the official boards\n-# [Phoenix 2] vs Jul 6 · swept Sunday", null),
            new IRichBotBlock[]
            {
                new RichBotText("🏆 **PUMBILITY top 10**\n" +
                                "` 1` **JEWEL** — 9,981 ↑2\n" +
                                "` 2` **ESI** — 9,940 –\n" +
                                "` 3` **NIMGO** — 9,902 ↓1\n" +
                                "` 4` **HYSTERIA** — 9,846 ↑5\n" +
                                "` 5` **PUMPKING** — 9,811 🆕"),
                new RichBotText("📈 **PUMBILITY movers**\n" +
                                "**HYSTERIA** #58 → **#41** · 9,120.45\n**KUMA** #112 → **#97** · 8,644.02"),
                new RichBotText("🧗 **Boards climbed**\n**PUMPKING** climbed 23 boards (+118)"),
                new RichBotText("🌍 **World firsts & new #1s**\n" +
                                "First **SSS+** on Paradoxx #DIFFICULTY|S26# — **ESI** · 995,120\n" +
                                "👑 New #1 on Gargoyle FS #DIFFICULTY|D25# — **NIMGO** · 998,110 (dethroned SPHAM)"),
                new RichBotText("🎟 **To make the top 1000**\n" +
                                "**50× AAA at Lv.20** · **50× SSS at Lv.17**")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores official mirror · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("This week", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards")),
                new RichBotLink("What it takes", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards/WhatItTakes"))
            });

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
                    "` 2` **ESI** — 993,204 #LETTERGRADE|SSS|False##PLATE|ExtremeGame# (Arrow Eclipse)\n" +
                    "` 3` **PUMPKING** — 988,917 #LETTERGRADE|SSPlus|False##PLATE|ExtremeGame#\n" +
                    "` 4` **WABBIT** — 983,660 #LETTERGRADE|SS|False##PLATE|SuperbGame# (Arrow Eclipse)\n" +
                    "` 5` **NIMGO** — 982,105 #LETTERGRADE|SS|False##PLATE|SuperbGame#")
            },
            $"#MIX|Phoenix2# Card 1 of 5 · 12 more charts had entries · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            Array.Empty<RichBotLink>());

    // One line per chart, co-ops first then by level, with a video link; one button.
    private static RichBotMessage WeeklyLineupCard(string marker) =>
        new(new RichBotSection("### This week's charts\n-# [Phoenix 2] one per level bucket", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText(
                    $"#DIFFICULTY|coop2# [District 1]({ChartBase}/00000000-0000-0000-0000-0000000000b0) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|S16# [Trashy Innocence]({ChartBase}/00000000-0000-0000-0000-0000000000b1) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|S20# [1949]({ChartBase}/00000000-0000-0000-0000-0000000000b2) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|D23# [Sarabande]({ChartBase}/00000000-0000-0000-0000-0000000000b3) - [Video]({Vid})")
            },
            $"#MIX|Phoenix2# Phoenix 2 · resets Monday midnight ET · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("Weekly Charts", new Uri("https://piuscores.arroweclip.se/WeeklyCharts")) });

    // Daily feed — yesterday's board (Limbo mocked) + today's chart in one card.
    private static RichBotMessage DailyFeedCard(string marker) =>
        new(new RichBotSection("### Daily Step\n-# [Phoenix 2] yesterday's board settled", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("**Yesterday — Trashy Innocence #DIFFICULTY|S19#**"),
                new RichBotText(
                    "` 1` **ESI** — 996,410 #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#\n" +
                    "` 2` **MELON** — 991,077 #LETTERGRADE|SSS|False##PLATE|ExtremeGame# (Arrow Eclipse)\n" +
                    "` 3` **TUSA** — 987,215 #LETTERGRADE|SSPlus|False##PLATE|SuperbGame#"),
                new RichBotDivider(),
                new RichBotSection(
                    $"**Today — [Bee]({ChartBase}/00000000-0000-0000-0000-0000000000c1) #DIFFICULTY|S7#**\n" +
                    "-# 🕯 **Limbo Day** — lowest passing score wins. No breaking.", SongArt)
            },
            $"#MIX|Phoenix2# Phoenix 2 · resets midnight ET · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("Daily Step board", new Uri("https://piuscores.arroweclip.se")) });

    // /piu chart — the chart-details card: the difficulty breakdown (scoring level + pass
    // tier), the skill fingerprint, and similar charts by skill, all linked.
    private static RichBotMessage ChartCard(string marker) =>
        new(new RichBotSection("### Witch Doctor — S18\n-# BanYa · 175 BPM · Phoenix 2", SongArt),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📊 Scoring level **18.6** (listed 18) · Pass **Medium**"),
                new RichBotText("🎯 Twist · Bracket · Run"),
                new RichBotDivider(),
                new RichBotText("**Similar charts**\n" +
                    $"#DIFFICULTY|D19# [Vacuum]({ChartBase}/00000000-0000-0000-0000-000000000002) — 84%\n" +
                    $"#DIFFICULTY|S18# [1949]({ChartBase}/00000000-0000-0000-0000-000000000003) — 79%\n" +
                    $"#DIFFICULTY|S19# [Bee]({ChartBase}/00000000-0000-0000-0000-000000000004) — 72%")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("Open chart page", new Uri($"{ChartBase}/00000000-0000-0000-0000-000000000001")) });

    // /piu random — a titled draw, one art row per chart, each linked with a video.
    private static RichBotMessage RandomCard(string marker) =>
        new(new RichBotSection("### Drew 3 charts\n-# Doubles · levels 20–23 · Phoenix 2", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|D22# [Sarabande]({ChartBase}/00000000-0000-0000-0000-000000000021) · [Video]({Vid})",
                    SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|D20# [Moonlight]({ChartBase}/00000000-0000-0000-0000-000000000022) · [Video]({Vid})",
                    SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|D23# [Gargoyle FS]({ChartBase}/00000000-0000-0000-0000-000000000023) · [Video]({Vid})",
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
                    $"#DIFFICULTY|S21# [District 1]({ChartBase}/00000000-0000-0000-0000-0000000000a1) · [Video]({Vid})\n" +
                    "-# [Expert Lv.7] — needs 985,000+, you're at 981,220", SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|D19# [Vacuum]({ChartBase}/00000000-0000-0000-0000-0000000000a2) · [Video]({Vid})\n" +
                    "-# [VACUUM Lv.3] — skill title progress, 942k/990k", SongArt),
                new RichBotSection(
                    $"#DIFFICULTY|S20# [1949]({ChartBase}/00000000-0000-0000-0000-0000000000a3) · [Video]({Vid})\n" +
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
