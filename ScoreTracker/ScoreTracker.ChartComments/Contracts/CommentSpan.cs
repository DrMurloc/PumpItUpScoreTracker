namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     One piece of a rendered comment. A comment crosses the vertical boundary as a flat list of
///     these, never as a string — Web walks them and emits elements, so <c>MarkupString</c> is
///     structurally unreachable rather than merely discouraged.
///     <para>
///         <see cref="IsTrusted" /> is resolved inside the vertical against the fixed host list and
///         the public tool URLs, so the presentation layer makes no policy call.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentSpan(CommentSpanKind Kind, string Text, string? Url = null, bool IsTrusted = false)
{
    public static CommentSpan OfText(string text)
    {
        return new CommentSpan(CommentSpanKind.Text, text);
    }

    public static CommentSpan OfLink(string url, bool isTrusted)
    {
        return new CommentSpan(CommentSpanKind.Link, url, url, isTrusted);
    }

    public static readonly CommentSpan Break = new(CommentSpanKind.LineBreak, string.Empty);
}

public enum CommentSpanKind
{
    Text,
    Link,

    /// <summary>
    ///     A newline the author typed. Flat rather than nested paragraphs: a comment is 500
    ///     characters, so a list of spans renders in one loop and cannot produce an unclosed block.
    /// </summary>
    LineBreak
}
