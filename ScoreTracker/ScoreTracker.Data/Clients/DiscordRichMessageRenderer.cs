using System.Text;
using Discord;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Pure translation from the provider-agnostic <see cref="RichBotMessage" /> to a
///     Discord Components V2 tree, plus the flattened plain-text fallback the per-channel
///     failure path sends through the legacy pipeline (which does its own token
///     replacement — the fallback keeps the raw tokens).
/// </summary>
public static class DiscordRichMessageRenderer
{
    // Discord's Components V2 budgets. The composing saga packs within these; the
    // clamps below are the defensive backstop (drop art first, then truncate).
    public const int MaxComponents = 40;
    public const int MaxTextLength = 4000;

    public static (MessageComponent Components, string FallbackText) Render(RichBotMessage message,
        Func<string, string> replaceTokens)
    {
        var container = new ContainerBuilder();
        if (message.AccentColor != null) container.WithAccentColor(new Color(message.AccentColor.Value));

        var fallback = new StringBuilder();
        // container + the ComponentBuilderV2 root's action row allowance are counted as used.
        var componentBudget = MaxComponents - 1;
        var textBudget = MaxTextLength;

        void AppendFallback(string markdown)
        {
            if (fallback.Length > 0) fallback.AppendLine();
            fallback.Append(markdown);
        }

        string Fit(string markdown)
        {
            var replaced = replaceTokens(markdown);
            if (replaced.Length > textBudget) replaced = replaced[..Math.Max(0, textBudget - 1)] + "…";
            textBudget -= replaced.Length;
            return replaced;
        }

        void AddText(string markdown)
        {
            if (componentBudget < 1 || textBudget <= 0) return;
            componentBudget -= 1;
            container.AddComponent(new TextDisplayBuilder().WithContent(Fit(markdown)));
        }

        void AddSection(RichBotSection section)
        {
            // A thumbnailed section costs 3 (section + text child + accessory); when the
            // budget can't afford art anymore, degrade to a bare text display.
            if (section.Thumbnail != null && componentBudget >= 3 && textBudget > 0)
            {
                componentBudget -= 3;
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder().WithContent(Fit(section.Markdown)))
                    .WithAccessory(new ThumbnailBuilder()
                        .WithMedia(new UnfurledMediaItemProperties(section.Thumbnail.ToString()))));
            }
            else
            {
                AddText(section.Markdown);
            }

            AppendFallback(section.Markdown);
        }

        if (message.Header != null) AddSection(message.Header);

        foreach (var block in message.Blocks)
            switch (block)
            {
                case RichBotSection section:
                    AddSection(section);
                    break;
                case RichBotText text:
                    AddText(text.Markdown);
                    AppendFallback(text.Markdown);
                    break;
                case RichBotDivider:
                    if (componentBudget >= 1)
                    {
                        componentBudget -= 1;
                        container.AddComponent(new SeparatorBuilder());
                    }

                    break;
            }

        if (!string.IsNullOrWhiteSpace(message.Footer))
        {
            AddText($"-# {message.Footer}");
            AppendFallback(message.Footer!);
        }

        var root = new ComponentBuilderV2().AddComponent(container);
        if (message.Links.Any())
        {
            var row = new ActionRowBuilder();
            foreach (var link in message.Links.Take(5))
                row.WithButton(ButtonBuilder.CreateLinkButton(link.Label, link.Url.ToString()));
            root.AddComponent(row);
        }

        return (root.Build(), fallback.ToString());
    }
}
