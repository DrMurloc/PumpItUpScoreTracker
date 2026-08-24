using System.Text.RegularExpressions;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Comment bodies are plain text. URLs autolink, newlines survive, and nothing else is
///     interpreted — no bold, no bullets, no code (docs/design/chart-comments §2).
///     <para>
///         A renderer is not optional even so: autolinking <em>is</em> parsing, and each link needs
///         its trust decided in here rather than in Web. What that buys over markdown is measured —
///         machine translation has no syntax to corrupt, the link-set invariance check stays set
///         equality on bare URLs, and the 500-character cap is 500 characters of comment rather
///         than 500 minus the asterisks.
///     </para>
/// </summary>
internal static partial class CommentText
{
    public const int MaxLength = 500;

    /// <summary>
    ///     Deliberately requires an explicit http(s) scheme, which is the whole scheme allowlist: a
    ///     <c>javascript:alert(1)</c> in a comment is never a link candidate in the first place. The
    ///     character class stops at whitespace and at the three characters that could only be there
    ///     because someone was aiming at a renderer.
    ///     <para>
    ///         The timeout is belt-and-braces rather than a known fix: this pattern has no nested
    ///         quantifier and cannot backtrack catastrophically. But it is run against arbitrary
    ///         text a stranger typed, on every comment render, and a bound costs nothing next to
    ///         being wrong about that.
    ///     </para>
    /// </summary>
    [GeneratedRegex(@"https?://[^\s<>""]+", RegexOptions.IgnoreCase, 100)]
    private static partial Regex UrlPattern();

    // Sentence punctuation that follows a URL far more often than it belongs to one.
    private static readonly char[] TrailingNoise = { '.', ',', ';', ':', '!', '?', '"', '\'' };

    /// <summary>
    ///     Canonical storage form: one newline convention, no trailing spaces, and no more than one
    ///     blank line in a row. Run before the length check, so the cap counts what will be stored
    ///     and rendered rather than whatever a paste happened to carry — and so forty blank lines
    ///     cannot be used as forty lines of screen.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            // One blank line is a paragraph break; the second onward is padding.
            if (trimmed.Length == 0 && kept.Count > 0 && kept[^1].Length == 0) continue;
            kept.Add(trimmed);
        }

        while (kept.Count > 0 && kept[0].Length == 0) kept.RemoveAt(0);
        while (kept.Count > 0 && kept[^1].Length == 0) kept.RemoveAt(kept.Count - 1);

        return string.Join("\n", kept).Trim();
    }

    /// <summary>
    ///     Renders stored text into spans. Every link arrives with <c>IsTrusted</c> already decided,
    ///     so Web never consults an allowlist and never sees the raw string.
    /// </summary>
    public static IReadOnlyList<CommentSpan> Parse(string? text, LinkTrust trust)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return Array.Empty<CommentSpan>();

        var spans = new List<CommentSpan>();
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) spans.Add(CommentSpan.Break);
            AppendLine(lines[i], trust, spans);
        }

        return spans;
    }

    private static void AppendLine(string line, LinkTrust trust, List<CommentSpan> spans)
    {
        var cursor = 0;
        foreach (Match match in UrlPattern().Matches(line))
        {
            var candidate = TrimTrailingNoise(match.Value);
            var uri = LinkTrust.TryParse(candidate);
            // An unparseable match stays inside the surrounding text run rather than becoming a
            // dead link — leaving it to the next iteration's cursor handles that for free.
            if (uri == null) continue;

            if (match.Index > cursor) spans.Add(CommentSpan.OfText(line[cursor..match.Index]));
            spans.Add(CommentSpan.OfLink(candidate, trust.IsTrusted(uri)));
            cursor = match.Index + candidate.Length;
        }

        if (cursor < line.Length) spans.Add(CommentSpan.OfText(line[cursor..]));
    }

    // "…watch https://youtu.be/abc." should link the video, not a video whose id ends in a period.
    // A closing bracket is kept when the URL opened one, which is how Wikipedia-shaped links survive.
    private static string TrimTrailingNoise(string url)
    {
        var end = url.Length;
        while (end > 0)
        {
            var last = url[end - 1];
            if (TrailingNoise.Contains(last)) { end--; continue; }
            if (last == ')' && url.AsSpan(0, end).Count('(') < url.AsSpan(0, end).Count(')')) { end--; continue; }
            break;
        }

        return url[..end];
    }

    // A fixed list of known trackers and nothing heuristic: stripping a parameter a site
    // actually needs breaks the link, so anything not listed stays.
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
        { "si", "fbclid", "gclid", "dclid", "msclkid", "twclid", "ttclid", "igshid", "yclid", "mc_cid", "mc_eid" };

    /// <summary>
    ///     Removes known tracking parameters (every <c>utm_*</c>, YouTube's <c>si</c>, the ad
    ///     click ids, Mailchimp's pair) from each link in already-normalized text. Runs at save,
    ///     so the stored text itself is rewritten — the author sees the cleaned link on edit, and
    ///     no tracker ever reaches storage, another reader, or the translation pipeline.
    /// </summary>
    public static string StripTrackingParameters(string normalized)
    {
        return RewriteLinks(normalized, StripTracking);
    }

    /// <summary>
    ///     Lifts every link out for translation, leaving a <see cref="TranslationMarkers" />
    ///     marker in each one's place — the model is told the markers are links and never sees a
    ///     URL. The level is picked against this text, so an author who typed a marker-shaped
    ///     string cannot collide with the real ones.
    /// </summary>
    public static MarkedCommentText ExtractLinks(string normalized)
    {
        var level = TranslationMarkers.PickLevel(normalized);
        var links = new List<string>();
        var marked = RewriteLinks(normalized, url =>
        {
            links.Add(url);
            return TranslationMarkers.Marker(links.Count, level);
        });

        return new MarkedCommentText(marked, links, level);
    }

    /// <summary>
    ///     The links this text renders, exactly as the parser would link them — the authoritative
    ///     vocabulary for the set-equality check on a translation. A rendering whose set differs
    ///     from its source's is never stored.
    /// </summary>
    public static IReadOnlyList<string> LinksIn(string normalized)
    {
        var links = new List<string>();
        RewriteLinks(normalized, url =>
        {
            links.Add(url);
            return url;
        });

        return links;
    }

    public static bool LinkSetsMatch(string original, string rendering)
    {
        return new HashSet<string>(LinksIn(original), StringComparer.Ordinal)
            .SetEquals(LinksIn(rendering));
    }

    /// <summary>
    ///     One walk over the text's links, applying <paramref name="rewrite" /> to each — the
    ///     same match, the same noise trim, and the same parseability bar as
    ///     <see cref="Parse" />, so every consumer of "a link" means the same thing by it.
    /// </summary>
    private static string RewriteLinks(string text, Func<string, string> rewrite)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var cursor = 0;
        foreach (Match match in UrlPattern().Matches(text))
        {
            var candidate = TrimTrailingNoise(match.Value);
            if (LinkTrust.TryParse(candidate) == null) continue;

            builder.Append(text, cursor, match.Index - cursor);
            builder.Append(rewrite(candidate));
            cursor = match.Index + candidate.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);

        return builder.ToString();
    }

    private static string StripTracking(string url)
    {
        var fragmentAt = url.IndexOf('#');
        var fragment = fragmentAt < 0 ? string.Empty : url[fragmentAt..];
        var beforeFragment = fragmentAt < 0 ? url : url[..fragmentAt];

        var queryAt = beforeFragment.IndexOf('?');
        if (queryAt < 0) return url;

        var kept = beforeFragment[(queryAt + 1)..]
            .Split('&')
            .Where(parameter =>
            {
                var name = parameter.Split('=', 2)[0];
                return !name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                       && !TrackingParameters.Contains(name);
            })
            .ToArray();

        return kept.Length == 0
            ? beforeFragment[..queryAt] + fragment
            : beforeFragment[..queryAt] + "?" + string.Join("&", kept) + fragment;
    }
}

/// <summary>
///     A comment body with its links lifted to markers, ready for the translation pipeline —
///     and the way back: <see cref="Substitute" /> puts the links into a returned rendering.
///     The caller still runs <see cref="CommentText.LinkSetsMatch" /> on the substituted result;
///     substitution is mechanical, the check is the guarantee.
/// </summary>
internal sealed record MarkedCommentText(string Text, IReadOnlyList<string> Links, int MarkerLevel)
{
    public string Substitute(string rendering)
    {
        var result = rendering;
        for (var i = 0; i < Links.Count; i++)
            result = result.Replace(TranslationMarkers.Marker(i + 1, MarkerLevel), Links[i]);

        return result;
    }
}
