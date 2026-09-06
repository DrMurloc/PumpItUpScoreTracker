using System.Diagnostics.CodeAnalysis;
using System.Text;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The one parser behind both API schemes and the rate limiter's rejection hook. The schemes'
///     own tests prove what a credential means; these prove what was presented is read the same
///     way by all three.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ApiCredentialTests
{
    private static string Basic(string user, string password)
    {
        return "Basic " + Convert.ToBase64String(
            Encoding.GetEncoding("iso-8859-1").GetBytes($"{user}:{password}"));
    }

    [Fact]
    public void ABearerHeaderYieldsItsTokenTrimmed()
    {
        var credential = ApiCredential.Parse("Bearer   piu_scores_live_abc  ");

        Assert.Null(credential.Failure);
        Assert.Equal(ApiCredentialKind.Bearer, credential.Kind);
        Assert.Equal("piu_scores_live_abc", credential.Secret);
    }

    [Fact]
    public void ABasicHeaderYieldsThePasswordAndIgnoresTheUsername()
    {
        var credential = ApiCredential.Parse(Basic("anything", "hunter2"));

        Assert.Null(credential.Failure);
        Assert.Equal(ApiCredentialKind.Basic, credential.Kind);
        Assert.Equal("hunter2", credential.Secret);
    }

    /// <summary>v1's decode, kept to the byte: a token with a non-ASCII username still reads.</summary>
    [Fact]
    public void ABasicHeaderDecodesAsLatin1()
    {
        var credential = ApiCredential.Parse(Basic("Jürgen", "secret"));

        Assert.Equal("secret", credential.Secret);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Digest whatever")]
    [InlineData("Basic not-base64!")]
    public void AnythingElseFailsWithAReason(string? header)
    {
        var credential = ApiCredential.Parse(header);

        Assert.NotNull(credential.Failure);
        Assert.Equal(string.Empty, credential.Secret);
    }

    /// <summary>Two colons is not a username and a password; neither is none.</summary>
    [Fact]
    public void BasicCredentialsMustSplitIntoExactlyTwo()
    {
        Assert.NotNull(ApiCredential.Parse(Basic("a:b", "c")).Failure);
        Assert.NotNull(ApiCredential.Parse("Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("nocolon"))).Failure);
    }
}
