using System.Text.RegularExpressions;
using ScoreTracker.ChartComments.Contracts;

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
internal static class CommentText
{
    public const int MaxLength = 500;

    // Deliberately requires an explicit http(s) scheme, which is the whole scheme allowlist: a
    // "javascript:alert(1)" in a comment is never a link candidate in the first place. The
    // character class stops at whitespace and at the three characters that could only be there
    // because someone was aiming at a renderer.
    private static readonly Regex UrlPattern =
        new(@"https?://[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        foreach (Match match in UrlPattern.Matches(line))
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
}
