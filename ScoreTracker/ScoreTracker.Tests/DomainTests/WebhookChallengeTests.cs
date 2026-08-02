using ScoreTracker.CommunityTools.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What counts as an endpoint echoing our challenge back. Generous on shape, exact on content —
///     a maker who returns the token wrapped in their framework's json helper has done the thing we
///     asked, and being pedantic about it buys nothing but support messages.
/// </summary>
public sealed class WebhookChallengeTests
{
    [Theory]
    [InlineData("vfy_abc123")]
    [InlineData("  vfy_abc123  ")]
    [InlineData("vfy_abc123\n")]
    [InlineData("\"vfy_abc123\"")]
    [InlineData("{\"challenge\":\"vfy_abc123\"}")]
    public void TheseAllCountAsAnEcho(string body)
    {
        Assert.True(WebhookChallenge.Echoes(body, "vfy_abc123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("OK")]
    [InlineData("vfy_somethingelse")]
    // The prefix of the real token is not the token — a substring match on the bare value would
    // accept a truncated echo, which is the one way a lazy handler could pass by accident.
    [InlineData("vfy_abc")]
    public void TheseDoNot(string? body)
    {
        Assert.False(WebhookChallenge.Echoes(body, "vfy_abc123"));
    }

    [Fact]
    public void AMintedChallengeIsPrefixedAndUnique()
    {
        var first = WebhookChallenge.Mint();
        var second = WebhookChallenge.Mint();

        Assert.StartsWith("vfy_", first);
        Assert.NotEqual(first, second);
    }
}
