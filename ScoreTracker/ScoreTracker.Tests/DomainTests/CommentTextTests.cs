using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommentTextTests
{
    private static IReadOnlyList<CommentSpan> Parse(string text, params string[] toolHosts)
    {
        return CommentText.Parse(text, new LinkTrust(toolHosts));
    }

    [Fact]
    public void PlainTextIsOneSpanAndNothingElse()
    {
        var spans = Parse("The drill at 2:01 is the whole chart.");

        var span = Assert.Single(spans);
        Assert.Equal(CommentSpanKind.Text, span.Kind);
        Assert.Equal("The drill at 2:01 is the whole chart.", span.Text);
    }

    [Theory]
    // Every one of these is markdown a reader might type and must see back verbatim. If any of
    // it is ever interpreted, existing comments change meaning retroactively.
    [InlineData("**drill** at 2:01")]
    [InlineData("_hold that_ and you pass")]
    [InlineData("- first\n- second")]
    [InlineData("# not a heading")]
    [InlineData("use `2:01` as the marker")]
    [InlineData("[label](https://example.com)")]
    public void MarkdownIsNotInterpreted(string text)
    {
        var spans = Parse(text);

        // The bracket case autolinks its bare URL, which is correct — what must not happen is the
        // brackets vanishing into a label.
        Assert.Equal(text, string.Concat(spans.Select(s => s.Kind == CommentSpanKind.LineBreak ? "\n" : s.Text)));
    }

    [Fact]
    public void ABareUrlBecomesALinkAndKeepsTheTextAroundIt()
    {
        var spans = Parse("Best run: https://youtu.be/kQw8ZmVn4rE — copy the footwork.");

        Assert.Collection(spans,
            s => Assert.Equal("Best run: ", s.Text),
            s =>
            {
                Assert.Equal(CommentSpanKind.Link, s.Kind);
                Assert.Equal("https://youtu.be/kQw8ZmVn4rE", s.Url);
                Assert.True(s.IsTrusted);
            },
            s => Assert.Equal(" — copy the footwork.", s.Text));
    }

    [Fact]
    public void AnUnknownHostStillLinksButIsNotTrusted()
    {
        var link = Assert.Single(Parse("https://stepcharts.example.net/x"),
            s => s.Kind == CommentSpanKind.Link);

        Assert.False(link.IsTrusted);
    }

    [Fact]
    public void SentencePunctuationAfterAUrlIsNotPartOfIt()
    {
        var spans = Parse("Watch https://youtu.be/abc.");

        Assert.Equal("https://youtu.be/abc", spans[1].Url);
        Assert.Equal(".", spans[2].Text);
    }

    [Fact]
    public void ABracketTheUrlOpenedItselfIsKept()
    {
        var link = Assert.Single(Parse("https://example.com/a_(b)"),
            s => s.Kind == CommentSpanKind.Link);

        Assert.Equal("https://example.com/a_(b)", link.Url);
    }

    [Fact]
    public void AJavascriptUrlIsNeverALink()
    {
        Assert.DoesNotContain(Parse("javascript:alert(1) and data:text/html,x"),
            s => s.Kind == CommentSpanKind.Link);
    }

    [Fact]
    public void NewlinesSurviveAsBreaks()
    {
        var spans = Parse("one\ntwo");

        Assert.Collection(spans,
            s => Assert.Equal("one", s.Text),
            s => Assert.Equal(CommentSpanKind.LineBreak, s.Kind),
            s => Assert.Equal("two", s.Text));
    }

    [Fact]
    public void RunsOfBlankLinesCollapseToOne()
    {
        // Forty newlines is forty lines of somebody else's screen.
        Assert.Equal("one\n\ntwo", CommentText.Normalize("one\n\n\n\n\n\ntwo"));
    }

    [Theory]
    [InlineData("  padded  ", "padded")]
    [InlineData("\n\nleading and trailing\n\n", "leading and trailing")]
    [InlineData("trailing spaces   \nsecond", "trailing spaces\nsecond")]
    [InlineData("windows\r\nnewlines", "windows\nnewlines")]
    [InlineData("old\rmac", "old\nmac")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeProducesTheStoredForm(string? input, string expected)
    {
        Assert.Equal(expected, CommentText.Normalize(input));
    }

    [Fact]
    public void EmptyTextParsesToNothingRatherThanAnEmptySpan()
    {
        Assert.Empty(Parse("   "));
    }
}
