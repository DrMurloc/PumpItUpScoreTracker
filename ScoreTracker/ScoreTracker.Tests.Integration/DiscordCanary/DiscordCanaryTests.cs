using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Clients;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

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

    // The other lab-channel tests (session snapshot PoC) share the canary's config.
    internal static string? CanaryToken => Token;
    internal static ulong? CanaryChannel => ChannelId;

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
        var channel = Assert.IsType<IMessageChannel>(await rest.GetChannelAsync(ChannelId.Value), exactMatch: false);
        var recent = (await channel.GetMessagesAsync(15).FlattenAsync()).ToArray();
        var mine = recent.Where(m => ComponentTexts(m.Components).Any(t => t.Contains(marker))).ToArray();

        Assert.Equal(2, mine.Length);
        await bot.Stop();
    }

    // Header art = the default avatar; row art = real song images from the CDN, so the
    // lab-channel gallery reads exactly like a production card.
    private static readonly Uri Avatar =
        new("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");

    // The two samples mirror the session-snapshot anatomy (design doc revision 2):
    // stats → achievements → notable scores, everything else a count.
    private static RichBotMessage SamplePassesCard(string marker)
    {
        return new RichBotMessage(
            new RichBotSection("### **Canary** — passed 2 · upscored 1\n-# S18–D19", Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY** 21,480 → **21,530** (+50)"),
                new RichBotDivider(),
                new RichBotText("🏅 **[Intermediate Lv. 10]** completed\n" +
                                "🏅 [Advanced Lv. 3] 62% → **71%**\n" +
                                "🎉 #DIFFICULTY|s18# **All passed!**\n" +
                                "🏆 **#1** on Witch Doctor #DIFFICULTY|d19# weekly"),
                new RichBotDivider(),
                new RichBotSection(
                    "#DIFFICULTY|d19# **Witch Doctor**\n**970,207** #LETTERGRADE|S|False##PLATE|UltimateGame#\n-# 👑 #4 in your PUMBILITY · 🆕 First D19",
                    new Uri("https://piuimages.arroweclip.se/songs/WitchDoctor.png")),
                new RichBotSection(
                    "#DIFFICULTY|s18# **Turkey March -Minimal Tunes-**\n**972,340** #LETTERGRADE|SSS|False##PLATE|SuperbGame#\n-# 🏅 [DRILL] Lv.4 (972k/990k) · 📊 #3 of 47 peers",
                    new Uri("https://piuimages.arroweclip.se/songs/TurkeyMarchMinimalTunes.png")),
                new RichBotText("-# More scores\n#DIFFICULTY|s16# Bad Apple — **945,120** #LETTERGRADE|SS|False##PLATE|MarvelousGame#"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|d19# 84/141 · #DIFFICULTY|s18# 182/195")
            },
            $"#MIX|Phoenix# Phoenix · PIU Scores · {marker}",
            MixEnum.Phoenix.GetAccentColor(),
            new[] { new RichBotLink("See more", new Uri("https://piuscores.arroweclip.se")) });
    }

    private static RichBotMessage SampleDigestCard(string marker)
    {
        return new RichBotMessage(
            new RichBotSection(
                "### [Phoenix 2] **Canary** — passed 1,872 · upscored 141\n-# S1–S22 · D4–D23 · CO-OP",
                Avatar),
            new IRichBotBlock[]
            {
                new RichBotDivider(),
                new RichBotText("📈 **PUMBILITY** 0 → **18,437** (+18,437)"),
                new RichBotDivider(),
                new RichBotText("🏅 **[Intermediate Lv. 1]** completed\n…and 4 more titles\n" +
                                "🎉 #DIFFICULTY|s13# **All passed!**"),
                new RichBotDivider(),
                new RichBotSection(
                    "#DIFFICULTY|d20# **Removable Disk0**\n**962,410** #LETTERGRADE|AAAPlus|False##PLATE|FairGame#\n-# 👑 #7 in your PUMBILITY",
                    new Uri("https://piuimages.arroweclip.se/songs/RemovableDisk0.png")),
                new RichBotText("-# More scores\n" +
                                "#DIFFICULTY|d19# Yog-Sothoth — **951,020** #LETTERGRADE|SS|False##PLATE|SuperbGame#\n" +
                                "#DIFFICULTY|s21# Errorcode 0 — **933,400** #LETTERGRADE|S|False##PLATE|MarvelousGame#"),
                new RichBotText("+2,008 more: D23 ×12, S22 ×48, S21 ×95, CO-OP ×31"),
                new RichBotDivider(),
                new RichBotText("#DIFFICULTY|s13# 153/153 · #DIFFICULTY|s14# 148/151")
            },
            $"#MIX|Phoenix2# Phoenix2 · PIU Scores · {marker}",
            MixEnum.Phoenix2.GetAccentColor(),
            new[] { new RichBotLink("See more", new Uri("https://piuscores.arroweclip.se")) });
    }

    // Shared with the real-session showcase, which reads its cards back the same way.
    internal static IEnumerable<string> ComponentTexts(IEnumerable<IMessageComponent> components)
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
