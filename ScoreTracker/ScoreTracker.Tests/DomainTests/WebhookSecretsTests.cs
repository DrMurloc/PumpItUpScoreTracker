using System;
using ScoreTracker.CommunityTools.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What counts as an endpoint proving it is the maker's.
///     <para>
///         Generous on shape, exact on content — a maker who returns the secret wrapped in their
///         framework's json helper has done the thing we asked, and being pedantic about it buys
///         nothing but support messages. What it is <b>not</b> generous about is knowledge: the
///         secret never leaves our side, so answering with it is the whole proof.
///     </para>
/// </summary>
public sealed class WebhookSecretsTests
{
    private const string Secret = "vfy_abc123";
    private static readonly string Hash = WebhookSecrets.HashOf(Secret);

    [Theory]
    [InlineData("vfy_abc123")]
    [InlineData("  vfy_abc123  ")]
    [InlineData("vfy_abc123\n")]
    [InlineData("\"vfy_abc123\"")]
    [InlineData("{\"secret\":\"vfy_abc123\"}")]
    [InlineData("{\"ok\":true,\"challenge\":\"vfy_abc123\"}")]
    public void TheseAllCountAsAnAnswer(string body)
    {
        Assert.True(WebhookSecrets.Answers(body, Hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("OK")]
    [InlineData("vfy_somethingelse")]
    // The prefix of the real secret is not the secret — a substring match on the bare value would
    // accept a truncated answer, which is the one way a lazy handler could pass by accident.
    [InlineData("vfy_abc")]
    public void TheseDoNot(string? body)
    {
        Assert.False(WebhookSecrets.Answers(body, Hash));
    }

    /// <summary>
    ///     The hole the old scheme had, written down as a test. We used to POST a challenge and
    ///     accept it echoed back, so anything that could receive our request could pass — including
    ///     whatever a hijacked DNS record pointed at. Now the request carries nothing to echo, and a
    ///     handler that mirrors its own request body proves only that it can mirror.
    /// </summary>
    [Theory]
    [InlineData("{\"type\":\"url_verification\"}")]
    [InlineData("url_verification")]
    [InlineData("{\"type\":\"url_verification\",\"challenge\":\"vfy_whatever\"}")]
    public void EchoingOurOwnRequestBackProvesNothing(string body)
    {
        Assert.False(WebhookSecrets.Answers(body, Hash));
    }

    [Fact]
    public void AGeneratedSecretIsPrefixedAndUnique()
    {
        var first = WebhookSecrets.MintVerificationSecret();
        var second = WebhookSecrets.MintVerificationSecret();

        Assert.StartsWith("vfy_", first);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    ///     Trimmed on both sides of the comparison, so a maker who registers a secret with a stray
    ///     space and returns it without one is not left staring at a failure they cannot see.
    /// </summary>
    [Fact]
    public void SurroundingWhitespaceIsNotPartOfTheSecret()
    {
        Assert.Equal(WebhookSecrets.HashOf(Secret), WebhookSecrets.HashOf("  " + Secret + "  "));
    }

    [Fact]
    public void TheHashIsNotTheSecret()
    {
        Assert.DoesNotContain(Secret, Hash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, Hash.Length);
    }
}

/// <summary>
///     Where a webhook may point. Verification POSTs from our server, inside our network, so an
///     unguarded target turns the feature into an internal-network probe with a status-code oracle.
/// </summary>
public sealed class WebhookTargetTests
{
    [Theory]
    [InlineData("https://planner.example/hook", true)]
    [InlineData("http://planner.example/hook", true)]
    [InlineData("ftp://planner.example/hook", false)]
    [InlineData("file:///etc/passwd", false)]
    public void OnlyHttpSchemesAreUsable(string url, bool expected)
    {
        Assert.Equal(expected, WebhookTarget.HasUsableScheme(new Uri(url)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.1")]
    // The cloud metadata range. On some providers this hands out credentials.
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    public void PrivateAddressesAreRefused(string address)
    {
        Assert.True(WebhookTarget.IsPrivate(System.Net.IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void PublicAddressesAreFine(string address)
    {
        Assert.False(WebhookTarget.IsPrivate(System.Net.IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("http://localhost:5000/hook")]
    [InlineData("http://api.localhost/hook")]
    [InlineData("http://box.local/hook")]
    [InlineData("http://svc.internal/hook")]
    public void PrivateSoundingNamesAreRefusedWithoutResolving(string url)
    {
        Assert.True(WebhookTarget.HasPrivateHostname(new Uri(url)));
    }

    [Fact]
    public void AnOrdinaryHostnameIsNotRefusedByName()
    {
        Assert.False(WebhookTarget.HasPrivateHostname(new Uri("https://planner.example/hook")));
    }
}
