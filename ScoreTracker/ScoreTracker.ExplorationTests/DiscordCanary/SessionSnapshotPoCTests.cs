using Discord;
using Discord.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ExplorationTests.DiscordCanary;

/// <summary>
///     Proof-of-concept for the single-message session snapshot (owner direction,
///     2026-07-05): stats changes → achievements → notable scores in ONE card, replacing
///     the passed/upscored/ratings/titles message stack. Three hand-built scenarios —
///     the exact Tomatonium session from the owner's "info dump" screenshot, the owner's
///     real June 19 session (deep link resolves to the stamped session), and an
///     initial-import digest. Posts to the lab channel for visual review; the pipeline
///     work only starts once the owner signs off on this shape.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SessionSnapshotPoCTests
{
    private const string Site = "https://piuscores.arroweclip.se";

    private static readonly Uri Avatar =
        new("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");

    [DiscordCanaryFact]
    public async Task PostsSessionSnapshotProofOfConceptCards()
    {
        var marker = $"snapshot PoC {Guid.NewGuid():N}";
        using var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration
            {
                BotToken = DiscordCanaryTests.CanaryToken!, RichScoreMessages = true
            }));
        await bot.Start();
        var ready = new TaskCompletionSource();
        bot.WhenReady(() =>
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await bot.SendRichMessages(
            new[] { UpscoreSession(marker), QuietSession(marker), ImportDigest(marker) },
            new[] { DiscordCanaryTests.CanaryChannel!.Value });

        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, DiscordCanaryTests.CanaryToken);
        var channel = Assert.IsType<IMessageChannel>(
            await rest.GetChannelAsync(DiscordCanaryTests.CanaryChannel.Value), exactMatch: false);
        var recent = (await channel.GetMessagesAsync(15).FlattenAsync()).ToArray();
        var mine = recent.Where(m =>
            DiscordCanaryTests.ComponentTexts(m.Components).Any(t => t.Contains(marker))).ToArray();

        Assert.Equal(3, mine.Length);
        await bot.Stop();
    }

    [DiscordCanaryFact]
    public async Task PostsEsiSessionRender()
    {
        var marker = $"snapshot PoC {Guid.NewGuid():N}";
        using var bot = new DiscordBotClient(NullLogger<DiscordBotClient>.Instance,
            Options.Create(new DiscordConfiguration
            {
                BotToken = DiscordCanaryTests.CanaryToken!, RichScoreMessages = true
            }));
        await bot.Start();
        var ready = new TaskCompletionSource();
        bot.WhenReady(() =>
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await bot.SendRichMessages(new[] { EsiSession(marker) },
            new[] { DiscordCanaryTests.CanaryChannel!.Value });

        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, DiscordCanaryTests.CanaryToken);
        var channel = Assert.IsType<IMessageChannel>(
            await rest.GetChannelAsync(DiscordCanaryTests.CanaryChannel.Value), exactMatch: false);
        var recent = (await channel.GetMessagesAsync(10).FlattenAsync()).ToArray();
        Assert.Contains(recent, m =>
            DiscordCanaryTests.ComponentTexts(m.Components).Any(t => t.Contains(marker)));
        await bot.Stop();
    }

    /// <summary>
    ///     A real production session (17 changes, co-op included) rendered through the
    ///     snapshot ruleset — 5 notable rows survive, 12 compress, the paragon gain
    ///     renders as its own grade-named line, and combined competitive (+0.010) plus
    ///     the sub-floor noise are filtered. Flags are hand-inferred from the legacy
    ///     message; the real pipeline computes them.
    /// </summary>
    private static RichBotMessage EsiSession(string marker)
    {
        return new RichBotMessage(
            new RichBotSection("### **esi** — passed 5 · upscored 12\n-# S16–S23 · D18–D21 · CO-OP", Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **Singles competitive** 21.416 → **21.447** (+0.031)"),
                new RichBotDivider(),
                new RichBotText("🏅 **Intermediate Lv. 7** paragon → #LETTERGRADE|B|False#"),
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S23# **[SONIC BOOM]({Site}/Chart/7028986E-00E1-4E2C-AE25-8F31F085442C)**\n" +
                    "**922,198** #LETTERGRADE|AA|False##PLATE|RoughGame#\n" +
                    "-# 👑 PUMBILITY top 50 · ⬆ Raised competitive level",
                    new Uri("https://piuimages.arroweclip.se/songs/SonicBoom.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S23# **[Darkside of The Mind]({Site}/Chart/3378C127-2359-43DE-92F1-F2BEF6D9C24C)**\n" +
                    "**914,174** #LETTERGRADE|AA|False##PLATE|RoughGame#\n" +
                    "-# 👑 PUMBILITY top 50 · ⬆ Raised competitive level",
                    new Uri("https://piuimages.arroweclip.se/songs/DarksideOfTheMind.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S22# **[See]({Site}/Chart/6E79EFF8-04DC-450F-878B-88F699060E6F)** " +
                    "**963,636** (+26,436) #LETTERGRADE|AAPlus|False# → #LETTERGRADE|AAAPlus|False##PLATE|TalentedGame#\n" +
                    "-# ⬆ Raised competitive level",
                    new Uri("https://piuimages.arroweclip.se/songs/See.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S21# **[Dignity]({Site}/Chart/DFFADEE8-351B-4B8A-9A81-FC35680F5F26)** " +
                    "**948,548** (+27,704) #LETTERGRADE|AA|False# → #LETTERGRADE|AAPlus|False##PLATE|TalentedGame#\n" +
                    "-# 💥 Biggest gain of the session",
                    new Uri("https://piuimages.arroweclip.se/songs/Dignity.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S21# **[INVASION]({Site}/Chart/5B54020E-1730-4709-BBB2-82DB69DBDF35)** " +
                    "**970,995** (+4,466) #LETTERGRADE|AAAPlus|False# → #LETTERGRADE|S|False##PLATE|FairGame#\n" +
                    "-# 🏅 Title progress",
                    new Uri("https://piuimages.arroweclip.se/songs/INVASION.png")),
                // Co-ops always show (owner call): up to 3, pass-tier difficulty desc, text
                // rows once the 5 art slots are spent.
                new RichBotText(
                    $"#DIFFICULTY|CoOp3# [Yo! Say!! Fairy!!!]({Site}/Chart/892F0C6A-86FF-4377-9A00-6F3BDFCB549B) " +
                    "**998,491** (+5,831) #LETTERGRADE|SSS|False# → #LETTERGRADE|SSSPlus|False##PLATE|ExtremeGame#\n" +
                    $"#DIFFICULTY|CoOp3# [Allegro Furioso]({Site}/Chart/03657405-354F-4375-8679-D911EF153B28) " +
                    "**993,843** (+895) #LETTERGRADE|SSS|False##PLATE|MarvelousGame#\n" +
                    $"#DIFFICULTY|CoOp2# [Awakening]({Site}/Chart/5ACF4C94-7A0A-4C43-A0E4-F153D7C5338B) " +
                    "**981,612** (+10,092) #LETTERGRADE|S|False# → #LETTERGRADE|SS|False##PLATE|FairGame#"),
                new RichBotText("+9 more: S22, D21, D20, S18, D18, S17, S16, CO-OP ×2"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|S23# 29/56 (51.8%) · #DIFFICULTY|S18# 43/189 (22.8%) · " +
                                "#DIFFICULTY|S17# 13/196 (6.6%) · #DIFFICULTY|S16# 14/189 (7.4%)")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            0x1D9BCC,
            new[] { new RichBotLink("See more", new Uri(Site)) });
    }

    /// <summary>Scenario A — the exact session from the owner's screenshot, one card.</summary>
    private static RichBotMessage UpscoreSession(string marker)
    {
        return new RichBotMessage(
            new RichBotSection("### **Tomatonium** — upscored 9\n-# S20–S24", Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY Singles** 68,176 → **68,305** (+129)"),
                new RichBotDivider(),
                new RichBotText("🏆 **#1** on the Witch Doctor #DIFFICULTY|S21# weekly chart\n" +
                                "🏅 2 scores advanced title progress"),
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S21# **[Witch Doctor]({Site}/Chart/27E9444A-8EB4-4F07-A58F-E997B3977B1F)**\n" +
                    "**1,000,000** (+8,881) #LETTERGRADE|SSS|False# → #LETTERGRADE|SSSPlus|False##PLATE|PerfectGame#\n" +
                    "-# 👑 PUMBILITY top 50 · 📊 Top scores among peers",
                    new Uri("https://piuimages.arroweclip.se/songs/WitchDoctor.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S24# **[Human Extinction (PIU Edit.)]({Site}/Chart/13C4E895-A17B-4980-ACB9-AEA357F82FCC)**\n" +
                    "**978,562** (+21,708) #LETTERGRADE|AAPlus|False# → #LETTERGRADE|S|False##PLATE|TalentedGame#\n" +
                    "-# 💥 Biggest gain of the session",
                    new Uri("https://piuimages.arroweclip.se/songs/HumanExtinction.png")),
                new RichBotText("+7 more: S23, S21 ×3, D20 ×3")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            0x1D9BCC,
            new[] { new RichBotLink("See more", new Uri(Site)) });
    }

    /// <summary>
    ///     Scenario B — the owner's real June 19 session: no stats moved, nothing
    ///     completed, so those sections are simply absent. The button deep-links to the
    ///     session the showcase stamped into the local database.
    /// </summary>
    private static RichBotMessage QuietSession(string marker)
    {
        return new RichBotMessage(
            new RichBotSection("### **DrMurloc** — passed 7\n-# D20–D23 · S26", Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|S26# **[Paradoxx]({Site}/Chart/B408B6EE-EB43-416E-B377-78BBC494CAB7)**\n" +
                    "**885,966** #LETTERGRADE|AAPlus|False##PLATE|TalentedGame#\n" +
                    "-# 👑 PUMBILITY top 50 · 🏅 Title progress",
                    new Uri("https://piuimages.arroweclip.se/songs/Paradoxx.png")),
                new RichBotSection(
                    $"#DIFFICULTY|D22# **[Gargoyle - FULL SONG -]({Site}/Chart/483368DD-F349-417F-A447-B3ED857B19B3)**\n" +
                    "**974,060** #LETTERGRADE|S|False##PLATE|TalentedGame#\n" +
                    "-# 🏅 Title progress",
                    new Uri("https://piuimages.arroweclip.se/songs/GargoyleFULLSONG.png")),
                new RichBotSection(
                    $"#DIFFICULTY|D20# [Becouse of You]({Site}/Chart/01402C43-9208-4B5F-9225-6C8288EBD5D9)\n" +
                    "**999,218** #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#",
                    new Uri("https://piuimages.arroweclip.se/songs/BecouseOfYou.png")),
                new RichBotText("+4 more: D23 ×2, D20 ×2"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|D23# 84/141 (59.6%) · #DIFFICULTY|D20# 122/168 (72.6%)")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            0x1D9BCC,
            new[]
            {
                new RichBotLink("See more",
                    new Uri($"{Site}/Player/E38954C4-B1B1-418A-93F6-C4B25C98B713/Sessions" +
                            "?session=9BE61FE7-3EF9-412B-ACC9-0D0D6C6F04EF"))
            });
    }

    /// <summary>Scenario C — a 2,000-score initial import stays one calm card.</summary>
    private static RichBotMessage ImportDigest(string marker)
    {
        return new RichBotMessage(
            new RichBotSection(
                "### **hyperion** — passed 1,872 · upscored 141\n-# S1–S22 · D4–D23 — highest new pass D23",
                Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY** 0 → **18,437** (first import)"),
                new RichBotDivider(),
                new RichBotText("🏅 **Intermediate Lv.1–Lv.5** completed · +2 paragon\n" +
                                "🎉 #DIFFICULTY|S13# **All passed!** · 🎉 #DIFFICULTY|S14# **All passed!**\n" +
                                "…and 3 more milestones"),
                new RichBotDivider(),
                new RichBotSection(
                    $"#DIFFICULTY|D23# **[INVASION]({Site}/Chart/EB9A0EE7-0675-42F7-B127-0B150655CE75)**\n" +
                    "**956,792** #LETTERGRADE|S|False##PLATE|FairGame#\n" +
                    "-# 👑 PUMBILITY top 50 · 🆕 Folder debut",
                    new Uri("https://piuimages.arroweclip.se/songs/INVASION.png")),
                new RichBotSection(
                    $"#DIFFICULTY|D22# **[Energy Synergy Matrix]({Site}/Chart/E106CB73-44DB-4CCB-AEA6-F6352E3EEB8B)**\n" +
                    "**983,232** #LETTERGRADE|SS|False##PLATE|MarvelousGame#\n" +
                    "-# 👑 PUMBILITY top 50 · 📊 Top scores among peers",
                    new Uri("https://piuimages.arroweclip.se/songs/EnergySynergyMatrix.png")),
                new RichBotSection(
                    $"#DIFFICULTY|S18# **[Heliosphere]({Site}/Chart/68544705-B307-44D0-BEC0-ADDEDF1C852A)**\n" +
                    "**998,737** #LETTERGRADE|SSSPlus|False##PLATE|UltimateGame#\n" +
                    "-# 📊 Top scores among peers · 📁 Nearly complete folder",
                    new Uri("https://piuimages.arroweclip.se/songs/Heliosphere.png")),
                new RichBotText("…and 29 more highlights — the full list is on the session page"),
                new RichBotDivider(),
                new RichBotText(
                    "#DIFFICULTY|D23# 84/141 (59.6%) · #DIFFICULTY|S18# 182/195 (93.3%) · #DIFFICULTY|S13# 153/153 (100%)")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            0x1D9BCC,
            new[] { new RichBotLink("See more", new Uri(Site)) });
    }
}
