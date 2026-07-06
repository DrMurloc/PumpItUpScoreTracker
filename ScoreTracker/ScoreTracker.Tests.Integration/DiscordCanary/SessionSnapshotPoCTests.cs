using Discord;
using Discord.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Tests.Integration.DiscordCanary;

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
        var channel = Assert.IsAssignableFrom<IMessageChannel>(
            await rest.GetChannelAsync(DiscordCanaryTests.CanaryChannel.Value));
        var recent = (await channel.GetMessagesAsync(15).FlattenAsync()).ToArray();
        var mine = recent.Where(m =>
            DiscordCanaryTests.ComponentTexts(m.Components).Any(t => t.Contains(marker))).ToArray();

        Assert.Equal(3, mine.Length);
        await bot.Stop();
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
            0xE8C24A,
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
            0xAEB6C4,
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
            0xE8C24A,
            new[] { new RichBotLink("See more", new Uri(Site)) });
    }
}
