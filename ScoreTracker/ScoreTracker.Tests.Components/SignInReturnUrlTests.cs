using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Which addresses a sign-in may send a player back to. It is what stops a crafted link from
///     sending someone to another site after signing in, so every shape a browser reads as another
///     host is refused — and since setup applies it from inside a circuit, it cannot lean on the
///     framework's URL helper to do so.
/// </summary>
public sealed class SignInReturnUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/CommunityTools")]
    [InlineData("/CommunityTools/Invite/5f0c3c47-7b5e-4a33-9c56-0d1e2f3a4b5c")]
    [InlineData("/Account?tab=signin")]
    public void KeepsAPathOnThisSite(string url)
    {
        Assert.Equal(url, SignInReturnUrl.Sanitize(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("CommunityTools")]
    [InlineData("/Account\n")]
    public void RefusesAnythingThatLeavesThisSite(string? url)
    {
        Assert.Null(SignInReturnUrl.Sanitize(url));
    }

    /// <summary>
    ///     A signed-in visitor sent back to a sign-in page is bounced to their return address again,
    ///     and setup's Continue resumes the address — either one would send the player in a circle.
    /// </summary>
    [Theory]
    [InlineData("/Login")]
    [InlineData("/login/Discord")]
    [InlineData("/Welcome")]
    [InlineData("/PiuGameLogin?returnUrl=%2F")]
    [InlineData("/Setup?from=Discord")]
    public void RefusesTheSignInPagesAndSetup(string url)
    {
        Assert.Null(SignInReturnUrl.Sanitize(url));
    }

    [Fact]
    public void AppendsTheAddressEscapedAndLeavesAPathBareWithoutOne()
    {
        Assert.Equal("/Setup?from=Discord&returnUrl=%2FCommunityTools",
            SignInReturnUrl.Append("/Setup?from=Discord", "/CommunityTools"));
        Assert.Equal("/Login", SignInReturnUrl.Append("/Login", null));
    }
}
