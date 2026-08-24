using System;
using System.Linq;
using ScoreTracker.ChartComments.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The Slice 4 half of <see cref="CommentText" />: what leaves for the translation pipeline,
///     what comes back, and the tracking strip at save.
/// </summary>
public sealed class CommentTextTranslationTests
{
    [Theory]
    [InlineData("https://youtu.be/abc?si=xyz123", "https://youtu.be/abc")]
    [InlineData("https://youtu.be/abc?si=xyz&t=95", "https://youtu.be/abc?t=95")]
    [InlineData("https://example.com/p?utm_source=x&utm_medium=y&q=drill", "https://example.com/p?q=drill")]
    [InlineData("https://example.com/p?fbclid=abc#section", "https://example.com/p#section")]
    [InlineData("https://example.com/p?gclid=1&mc_cid=2&mc_eid=3", "https://example.com/p")]
    public void KnownTrackersAreStrippedFromALink(string dirty, string clean)
    {
        Assert.Equal($"watch {clean} now", CommentText.StripTrackingParameters($"watch {dirty} now"));
    }

    [Theory]
    // Anything not on the fixed list stays — stripping a parameter a site needs breaks the link.
    [InlineData("https://youtu.be/abc?t=95")]
    [InlineData("https://example.com/search?q=utm_source")]
    [InlineData("https://example.com/p?ref=nav&size=20")]
    public void UnlistedParametersSurvive(string url)
    {
        Assert.Equal(url, CommentText.StripTrackingParameters(url));
    }

    [Fact]
    public void TextWithoutLinksComesBackUntouched()
    {
        Assert.Equal("no links, just ⟦1⟧ vibes and si=fake",
            CommentText.StripTrackingParameters("no links, just ⟦1⟧ vibes and si=fake"));
    }

    [Fact]
    public void ExtractionLiftsEveryLinkToAMarkerInOrder()
    {
        var marked = CommentText.ExtractLinks(
            "run: https://youtu.be/abc and steps: https://piucenter.com/c/9");

        Assert.Equal("run: ⟦1⟧ and steps: ⟦2⟧", marked.Text);
        Assert.Equal(new[] { "https://youtu.be/abc", "https://piucenter.com/c/9" }, marked.Links);
    }

    [Fact]
    public void AnAuthorTypedMarkerPushesTheRealOnesToAHigherLevel()
    {
        var marked = CommentText.ExtractLinks("I typed ⟦1⟧ myself: https://youtu.be/abc");

        Assert.True(marked.MarkerLevel > 0);
        Assert.Contains("⟦1⟧ myself", marked.Text);
        Assert.DoesNotContain("https://", marked.Text);
    }

    [Fact]
    public void SubstitutionPutsTheLinksBackWhereverTheRenderingMovedThem()
    {
        var marked = CommentText.ExtractLinks("proof: https://youtu.be/abc end");

        Assert.Equal("al final https://youtu.be/abc es la prueba",
            marked.Substitute("al final ⟦1⟧ es la prueba"));
    }

    [Fact]
    public void TheTrailingNoiseRuleIsSharedSoAPeriodNeverEntersAMarker()
    {
        var marked = CommentText.ExtractLinks("watch https://youtu.be/abc.");

        Assert.Equal("watch ⟦1⟧.", marked.Text);
        Assert.Equal("https://youtu.be/abc", marked.Links.Single());
    }

    [Fact]
    public void LinkSetsMatchIsSetEqualityOnWhatWouldActuallyLink()
    {
        Assert.True(CommentText.LinkSetsMatch(
            "a https://youtu.be/abc b https://piucenter.com/c",
            "https://piucenter.com/c primero, luego https://youtu.be/abc"));
        Assert.False(CommentText.LinkSetsMatch(
            "a https://youtu.be/abc",
            "a https://youtu.be/abc y https://evil.example"));
        Assert.False(CommentText.LinkSetsMatch(
            "a https://youtu.be/abc",
            "sin enlaces"));
    }

    [Fact]
    public void ASmuggledLinkFailsTheMatchEvenAfterCleanSubstitution()
    {
        var marked = CommentText.ExtractLinks("see https://youtu.be/abc");
        var substituted = marked.Substitute("mira ⟦1⟧ en https://phish.example");

        Assert.False(CommentText.LinkSetsMatch("see https://youtu.be/abc", substituted));
    }
}

public sealed class CommentTranslationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEditWhileStillPendingReplacesFree()
    {
        Assert.True(CommentTranslationPolicy.MayQueueAfterEdit(false, Now.AddMinutes(-5), Now));
    }

    [Fact]
    public void ATranslatedCommentWaitsOutTheCooldown()
    {
        Assert.False(CommentTranslationPolicy.MayQueueAfterEdit(true, Now.AddHours(-23), Now));
        Assert.True(CommentTranslationPolicy.MayQueueAfterEdit(true, Now.AddHours(-24), Now));
    }

    [Fact]
    public void ATranslatedCommentThatWasSomehowNeverQueuedMayQueue()
    {
        Assert.True(CommentTranslationPolicy.MayQueueAfterEdit(true, null, Now));
    }
}
