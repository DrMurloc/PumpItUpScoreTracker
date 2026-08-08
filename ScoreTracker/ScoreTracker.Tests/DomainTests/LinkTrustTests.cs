using ScoreTracker.ChartComments.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The comment link gate. Everything here is a case where a sloppier rule would hand an
///     attacker the blue "this is fine" treatment on a host nobody vetted.
/// </summary>
public sealed class LinkTrustTests
{
    private static bool Trusted(string url, params string[] toolHosts)
    {
        var uri = LinkTrust.TryParse(url);
        Assert.NotNull(uri);

        return new LinkTrust(toolHosts).IsTrusted(uri!);
    }

    [Theory]
    [InlineData("https://youtu.be/kQw8ZmVn4rE")]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("http://reddit.com/r/piu")]
    [InlineData("https://piucenter.com/chart/baroque-virus-s20")]
    [InlineData("https://piuscores.arroweclip.se/Charts")]
    [InlineData("https://pumpout2020.anyhowstep.com/")]
    public void KnownDomainsAndTheirSubdomainsAreTrusted(string url)
    {
        Assert.True(Trusted(url));
    }

    [Theory]
    // The whole reason the match is on a dot boundary rather than EndsWith.
    [InlineData("https://youtube.com.evil.tld/watch")]
    [InlineData("https://notyoutube.com/watch")]
    [InlineData("https://evilyoutu.be/abc")]
    // A sibling of the pattern archive is not the pattern archive.
    [InlineData("https://anyhowstep.com/")]
    [InlineData("https://stepcharts.example.net/pattern/2201")]
    public void LookalikeHostsAreNotTrusted(string url)
    {
        Assert.False(Trusted(url));
    }

    [Fact]
    public void APublicToolIsTrustedOnItsExactHostOnly()
    {
        Assert.True(Trusted("https://tools.example.com/piu", "tools.example.com"));
        // A maker who owns one subdomain does not own the apex, let alone its neighbours.
        Assert.False(Trusted("https://evil.example.com/piu", "tools.example.com"));
        Assert.False(Trusted("https://example.com/piu", "tools.example.com"));
    }

    [Fact]
    public void ATrailingDotDoesNotDodgeTheComparison()
    {
        Assert.True(Trusted("https://youtu.be./abc"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path")]
    [InlineData("not a url at all")]
    public void OnlyAbsoluteHttpAndHttpsParse(string candidate)
    {
        Assert.Null(LinkTrust.TryParse(candidate));
    }
}
