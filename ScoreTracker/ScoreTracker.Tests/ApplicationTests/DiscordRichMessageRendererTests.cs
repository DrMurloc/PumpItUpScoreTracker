using System;
using System.Linq;
using Discord;
using ScoreTracker.Data.Clients;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordRichMessageRendererTests
{
    private static RichBotMessage Message(
        RichBotSection? header = null,
        IRichBotBlock[]? blocks = null,
        string? footer = null,
        uint? accent = null,
        RichBotLink[]? links = null)
    {
        return new RichBotMessage(header, blocks ?? Array.Empty<IRichBotBlock>(), footer, accent,
            links ?? Array.Empty<RichBotLink>());
    }

    [Fact]
    public void RendersContainerWithReplacedTokensAndAccent()
    {
        var message = Message(
            header: new RichBotSection("**alice** passed #DIFFICULTY|d23#", new Uri("https://img.invalid/a.png")),
            blocks: new IRichBotBlock[]
            {
                new RichBotText("#LETTERGRADE|SSSPlus# 999,150"),
                new RichBotDivider(),
                new RichBotText("stats line")
            },
            footer: "#MIX|Phoenix# Phoenix",
            accent: 0xE8C24A,
            links: new[] { new RichBotLink("View all recent scores", new Uri("https://site.invalid/p/1")) });

        var (components, _) = DiscordRichMessageRenderer.Render(message, s => s.Replace("#DIFFICULTY|d23#", "<:d23:1>")
            .Replace("#LETTERGRADE|SSSPlus#", "<:sss:2>").Replace("#MIX|Phoenix#", "<:px:3>"));

        var container = Assert.IsType<ContainerComponent>(components.Components.First());
        Assert.NotNull(container.AccentColor);
        var texts = Flatten(container).OfType<TextDisplayComponent>().Select(t => t.Content).ToArray();
        Assert.Contains(texts, t => t.Contains("<:d23:1>"));
        Assert.Contains(texts, t => t.Contains("<:sss:2>"));
        Assert.Contains(texts, t => t.Contains("<:px:3>"));
        Assert.DoesNotContain(texts, t => t.Contains("#DIFFICULTY|"));
        // Header section carries the thumbnail accessory; the link button rides an action row.
        Assert.Contains(Flatten(container), c => c is ThumbnailComponent);
        var row = Assert.IsType<ActionRowComponent>(components.Components.Last());
        var button = Assert.IsType<ButtonComponent>(row.Components.Single());
        Assert.Equal(ButtonStyle.Link, button.Style);
    }

    [Fact]
    public void FallbackKeepsRawTokensForTheLegacyPipeline()
    {
        var message = Message(
            header: new RichBotSection("**alice** passed:", null),
            blocks: new IRichBotBlock[] { new RichBotText("#DIFFICULTY|d23# Bee: 970,207") },
            footer: "Phoenix · PIU Scores");

        var (_, fallback) = DiscordRichMessageRenderer.Render(message, s => s.Replace("#DIFFICULTY|d23#", "X"));

        // The fallback goes through the legacy send path, which replaces tokens itself.
        Assert.Contains("#DIFFICULTY|d23#", fallback);
        Assert.Contains("**alice** passed:", fallback);
        Assert.Contains("Phoenix · PIU Scores", fallback);
    }

    [Fact]
    public void ComponentBudgetDropsArtBeforeContent()
    {
        // 30 thumbnailed sections would cost ~91 components; the clamp degrades the
        // overflow to text displays and nothing is lost from the fallback.
        var sections = Enumerable.Range(0, 30)
            .Select(i => (IRichBotBlock)new RichBotSection($"row {i}", new Uri($"https://img.invalid/{i}.png")))
            .ToArray();

        var (components, fallback) = DiscordRichMessageRenderer.Render(Message(blocks: sections), s => s);

        var container = Assert.IsType<ContainerComponent>(components.Components.First());
        Assert.True(CountComponents(container) <= DiscordRichMessageRenderer.MaxComponents,
            $"Rendered {CountComponents(container)} components");
        for (var i = 0; i < 30; i++) Assert.Contains($"row {i}", fallback);
    }

    [Fact]
    public void TextBudgetTruncatesInsteadOfOverflowing()
    {
        var blocks = Enumerable.Range(0, 5)
            .Select(i => (IRichBotBlock)new RichBotText(new string('x', 1500)))
            .ToArray();

        var (components, _) = DiscordRichMessageRenderer.Render(Message(blocks: blocks), s => s);

        var container = Assert.IsType<ContainerComponent>(components.Components.First());
        var total = Flatten(container).OfType<TextDisplayComponent>().Sum(t => t.Content.Length);
        Assert.True(total <= DiscordRichMessageRenderer.MaxTextLength, $"Rendered {total} chars");
    }

    private static IMessageComponent[] Flatten(IMessageComponent component)
    {
        return component switch
        {
            ContainerComponent container => container.Components
                .SelectMany(Flatten).Prepend(component).ToArray(),
            SectionComponent section => section.Components.SelectMany(Flatten)
                .Concat(section.Accessory != null ? Flatten(section.Accessory) : Array.Empty<IMessageComponent>())
                .Prepend(component).ToArray(),
            _ => new[] { component }
        };
    }

    private static int CountComponents(IMessageComponent component)
    {
        return Flatten(component).Length;
    }
}
