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
        var pass = new RenderPass(replaceTokens, message.AccentColor);
        if (message.Header != null) pass.AddSection(message.Header);
        foreach (var block in message.Blocks) pass.AddBlock(block);
        if (!string.IsNullOrWhiteSpace(message.Footer))
        {
            pass.AddText($"-# {message.Footer}");
            pass.AppendFallback(message.Footer!);
        }

        return (pass.Build(message.Links), pass.FallbackText);
    }

    /// <summary>One message's rendering state: component/text budgets and the fallback.</summary>
    private sealed class RenderPass
    {
        private readonly ContainerBuilder _container = new();
        private readonly StringBuilder _fallback = new();
        private readonly Func<string, string> _replaceTokens;
        // container + the ComponentBuilderV2 root's action row allowance are counted as used.
        private int _componentBudget = MaxComponents - 1;
        private int _textBudget = MaxTextLength;

        public RenderPass(Func<string, string> replaceTokens, uint? accentColor)
        {
            _replaceTokens = replaceTokens;
            if (accentColor != null) _container.WithAccentColor(new Color(accentColor.Value));
        }

        public string FallbackText => _fallback.ToString();

        public void AddBlock(IRichBotBlock block)
        {
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
                    AddDivider();
                    break;
            }
        }

        public void AddSection(RichBotSection section)
        {
            // A thumbnailed section costs 3 (section + text child + accessory); when the
            // budget can't afford art anymore, degrade to a bare text display.
            if (section.Thumbnail != null && _componentBudget >= 3 && _textBudget > 0)
            {
                _componentBudget -= 3;
                _container.AddComponent(new SectionBuilder()
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

        public void AddText(string markdown)
        {
            if (_componentBudget < 1 || _textBudget <= 0) return;
            _componentBudget -= 1;
            _container.AddComponent(new TextDisplayBuilder().WithContent(Fit(markdown)));
        }

        public void AppendFallback(string markdown)
        {
            if (_fallback.Length > 0) _fallback.AppendLine();
            _fallback.Append(markdown);
        }

        public MessageComponent Build(IReadOnlyList<RichBotLink> links)
        {
            var root = new ComponentBuilderV2().AddComponent(_container);
            if (links.Count > 0)
            {
                var row = new ActionRowBuilder();
                foreach (var link in links.Take(5))
                    row.WithButton(ButtonBuilder.CreateLinkButton(link.Label, link.Url.ToString()));
                root.AddComponent(row);
            }

            return root.Build();
        }

        private void AddDivider()
        {
            if (_componentBudget < 1) return;
            _componentBudget -= 1;
            _container.AddComponent(new SeparatorBuilder());
        }

        private string Fit(string markdown)
        {
            var replaced = _replaceTokens(markdown);
            if (replaced.Length > _textBudget) replaced = replaced[..Math.Max(0, _textBudget - 1)] + "…";
            _textBudget -= replaced.Length;
            return replaced;
        }
    }
}
