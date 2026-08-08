using Discord;
using Discord.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ExplorationTests.DiscordCanary;

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
    private const string PlayersBase = "https://piuscores.arroweclip.se/OfficialLeaderboards/Players";
    private const string Vid = "https://youtu.be/piu-sample-video";

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

    [DiscordCanaryFact]
    public Task PostsTheSessionSnapshotReclearCard() => PostAndVerify(SessionSnapshotCard);

    [DiscordCanaryFact]
    public Task PostsTheKoreanSampleCards() =>
        PostAndVerify(KoreanSessionSnapshotCard, KoreanWeeklyLineupCard, KoreanOfficialDigestCard);

    // The L-series localization, sampled in Korean: the session snapshot, the weekly
    // lineup, and the official digest exactly as a ko-KR-registered channel receives them
    // (strings verbatim from App.ko-KR.resx).
    private static RichBotMessage KoreanSessionSnapshotCard(string marker) =>
        new(new RichBotSection("### [Phoenix 2] **alice** — 패스 3 · 점수 갱신 1\n-# S18–S21", SongArt),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY** 21,480 → **21,530** (+50)"),
                new RichBotDivider(),
                new RichBotText("🏆 주간 District 1 #DIFFICULTY|S21# **#2**"),
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S21# **[Conflict]({ChartBase}/00000000-0000-0000-0000-0000000000d1)**\\*\n" +
                    "**991,204** #LETTERGRADE|SSS|False##PLATE|ExtremeGame#\n" +
                    "-# 👑 내 PUMBILITY #4 · 📊 동급 47명 중 #3", SongArt),
                new RichBotText(
                    "-# 그 외 점수\n" +
                    "#DIFFICULTY|S19# Trashy Innocence\\* — **984,120** #LETTERGRADE|SS|False##PLATE|SuperbGame#\n" +
                    "#DIFFICULTY|S18# Bee — **977,860** (+6,560) #LETTERGRADE|S|False# → #LETTERGRADE|SS|False##PLATE|SuperbGame#"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|S21# 3/42 · #DIFFICULTY|S18# 12/58")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · \\* = 재클리어 · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("더 보기",
                    new Uri($"{PlayerBase}/00000000-0000-0000-0000-0000000000d0/Sessions"))
            });

    private static RichBotMessage KoreanWeeklyLineupCard(string marker) =>
        new(new RichBotSection("### 이번 주 채보\n-# [Phoenix 2] 레벨 구간별 1개", null),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText(
                    $"#DIFFICULTY|coop2# [District 1]({ChartBase}/00000000-0000-0000-0000-0000000000b0) - [동영상]({Vid})\n" +
                    $"#DIFFICULTY|S16# [Trashy Innocence]({ChartBase}/00000000-0000-0000-0000-0000000000b2) - [동영상]({Vid})\n" +
                    $"#DIFFICULTY|D16# [Moonlight]({ChartBase}/00000000-0000-0000-0000-0000000000b3) - [동영상]({Vid})\n" +
                    $"#DIFFICULTY|S18# [Bee]({ChartBase}/00000000-0000-0000-0000-0000000000b4) - [동영상]({Vid})")
            },
            $"#MIX|Phoenix2# Phoenix 2 · 매주 월요일 자정(ET) 초기화 · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("주간 채보", new Uri("https://piuscores.arroweclip.se/WeeklyCharts")) });

    // The same reshaped card in Korean — the locale most likely to expose a length problem,
    // since CJK glyphs are double-width and the floor rows depend on the rank labels aligning.
    private static RichBotMessage KoreanOfficialDigestCard(string marker) =>
        new(new RichBotSection(
                "### 이번 주 공식 리더보드\n" +
                "-# [Phoenix 2] 7월 26일 대비 · 1,857명이 흔적을 남겼고 — Freedom Dive D27가 세계 최초 AAA+를 내줬습니다.",
                FreedomDiveArt),
            new IRichBotBlock[]
            {
                new RichBotText("등록 **26,487** · 활동 **1,857** · 첫 등장 **375**\n-# 신규 23,273 · 갱신 3,214"),
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY 최다 상승**\n" +
                                $"[RUN]({PlayersBase}?player=RUN%234678) **+2,914.44**（18,645.90）· #908 → **#41**"),
                new RichBotDivider(),
                new RichBotText("🧗 **채보 보드 최다 상승**\n" +
                                $"[MECCHAMILE]({PlayersBase}?player=MECCHAMILE%232227) 채보 보드 90개에서 **+11,990** · 신규 90"),
                new RichBotDivider(),
                new RichBotText("🎟 **각 순위 컷** — 싱글 50× AAA\n" +
                                "` #100` **Lv.24** · 18,283.13 ▲185\n" +
                                "`#1000` Lv.16 → **Lv.20** · 16,489.37 ▲1,270"),
                new RichBotDivider(),
                new RichBotText("🌍 **세계 최초 기록**\n" +
                                $"#DIFFICULTY|D27# **Freedom Dive** — 세계 최초 #LETTERGRADE|AAAPlus# — [FRANCO]({PlayersBase}?player=FRANCO%233928)\n" +
                                $"#DIFFICULTY|D26# **OVERNIGHT FLOWER** — 세계 최초 #LETTERGRADE|SSPlus# — [FEFEMZ]({PlayersBase}?player=FEFEMZ%231489)")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores 공식 미러 · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("이번 주", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards")),
                new RichBotLink("랭킹", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards/Rankings")),
                new RichBotLink("필요 조건", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards/WhatItTakes"))
            });

    // Official digest (D1 reshape) — the week's numbers first, then one marquee name per
    // category, the two PUMBILITY floors in AAA, and the world firsts closing it out ordered
    // by level. The header wears the top first's jacket; the hype sentence is its caption.
    // Sampled from the real Phoenix 2 sweep of 2026-08-02, which is what makes it a useful
    // canary: these are the lengths and the digit counts a live week actually produces.
    private static readonly Uri FreedomDiveArt =
        new("https://piuimages.arroweclip.se/songs/FreedomDive.png");

    private static RichBotMessage OfficialDigestCard(string marker) =>
        new(new RichBotSection(
                "### This week on the official boards\n" +
                "-# [Phoenix 2] vs Jul 26 · 1,857 players left their mark — and Freedom Dive D27 fell to its first AAA+.",
                FreedomDiveArt),
            new IRichBotBlock[]
            {
                new RichBotText("**26,487** board entries · **1,857** players active · **375** debuts\n" +
                                "-# 23,273 new · 3,214 upscored"),
                new RichBotDivider(),
                new RichBotText("📈 **Biggest PUMBILITY gain**\n" +
                                $"[RUN]({PlayersBase}?player=RUN%234678) **+2,914.44** to 18,645.90 · #908 → **#41**"),
                new RichBotDivider(),
                new RichBotText("🧗 **Biggest board climber**\n" +
                                $"[MECCHAMILE]({PlayersBase}?player=MECCHAMILE%232227) **+11,990 places** " +
                                "across 90 chart boards · 90 new"),
                new RichBotDivider(),
                new RichBotText("🎟 **What holds the rungs** — 50× AAA on singles\n" +
                                "` #100` **Lv.24** · 18,283.13 ▲185\n" +
                                "`#1000` Lv.16 → **Lv.20** · 16,489.37 ▲1,270"),
                new RichBotDivider(),
                new RichBotText("🌍 **World firsts**\n" +
                                $"#DIFFICULTY|D27# **Freedom Dive** — World First #LETTERGRADE|AAAPlus# — [FRANCO]({PlayersBase}?player=FRANCO%233928)\n" +
                                $"#DIFFICULTY|D26# **OVERNIGHT FLOWER** — World First #LETTERGRADE|SSPlus# — [FEFEMZ]({PlayersBase}?player=FEFEMZ%231489)\n" +
                                $"#DIFFICULTY|D25# **Legendary Dominion** — World First #LETTERGRADE|SSSPlus# — [FEFEMZ]({PlayersBase}?player=FEFEMZ%231489)\n" +
                                $"#DIFFICULTY|D24# **Digitalis** — World First #PLATE|PerfectGame# — [FEFEMZ]({PlayersBase}?player=FEFEMZ%231489)\n" +
                                $"#DIFFICULTY|D24# **Unfelicitas** — World First #PLATE|PerfectGame# — [DA3RXM]({PlayersBase}?player=DA3RXM%233573)")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores official mirror · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("This Week", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards")),
                new RichBotLink("Rankings", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards/Rankings")),
                new RichBotLink("What It Takes", new Uri("https://piuscores.arroweclip.se/OfficialLeaderboards/WhatItTakes"))
            });

    // Session snapshot — the score-batch card, here carrying cross-mix reclears (F7): a new
    // pass the player already cleared in another mix trails an asterisk, and the footer
    // footnotes it. The flagged art row shows the mark on a bold link; a plain "More scores"
    // row shows it too. Everything else mirrors the existing card unchanged.
    private const string PlayerBase = "https://piuscores.arroweclip.se/Player";

    private static RichBotMessage SessionSnapshotCard(string marker) =>
        new(new RichBotSection("### [Phoenix 2] **alice** — passed 3 · upscored 1\n-# S18–S21", SongArt),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY** 21,480 → **21,530** (+50)"),
                new RichBotDivider(),
                new RichBotText("🏆 **#2** on District 1 #DIFFICULTY|S21# weekly"),
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S21# **[Conflict]({ChartBase}/00000000-0000-0000-0000-0000000000d1)**\\*\n" +
                    "**991,204** #LETTERGRADE|SSS|False##PLATE|ExtremeGame#\n" +
                    "-# 👑 #4 in your PUMBILITY · 📊 #3 of 47 peers", SongArt),
                new RichBotText(
                    "-# More scores\n" +
                    "#DIFFICULTY|S19# Trashy Innocence\\* — **984,120** #LETTERGRADE|SS|False##PLATE|SuperbGame#\n" +
                    "#DIFFICULTY|S18# Bee — **977,860** (+6,560) #LETTERGRADE|S|False# → #LETTERGRADE|SS|False##PLATE|SuperbGame#"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|S21# 3/42 · #DIFFICULTY|S18# 12/58")
            },
            $"#MIX|Phoenix2# Phoenix 2 · PIU Scores · \\* = reclears · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[]
            {
                new RichBotLink("See more",
                    new Uri($"{PlayerBase}/00000000-0000-0000-0000-0000000000d0/Sessions"))
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
                    $"#DIFFICULTY|coop3# [Bad Apple]({ChartBase}/00000000-0000-0000-0000-0000000000b1) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|S16# [Trashy Innocence]({ChartBase}/00000000-0000-0000-0000-0000000000b2) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|D16# [Moonlight]({ChartBase}/00000000-0000-0000-0000-0000000000b3) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|S18# [Bee]({ChartBase}/00000000-0000-0000-0000-0000000000b4) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|D19# [Vacuum]({ChartBase}/00000000-0000-0000-0000-0000000000b5) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|S20# [1949]({ChartBase}/00000000-0000-0000-0000-0000000000b6) - [Video]({Vid})\n" +
                    $"#DIFFICULTY|D21# [Uglier Dee]({ChartBase}/00000000-0000-0000-0000-0000000000b7) - [Video]({Vid})")
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
