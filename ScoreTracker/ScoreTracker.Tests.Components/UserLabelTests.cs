using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     UserLabel renders in reused positions — the board dialogs swap whole row sets in place
///     when their type segment switches — so its country wash must follow the CURRENT user,
///     not the first one it ever saw. The monthly dialog's type switch crashed on exactly this.
/// </summary>
public sealed class UserLabelTests : ComponentTestBase
{
    private readonly Mock<IUserRepository> _users = new();

    public UserLabelTests()
    {
        _users.Setup(u => u.GetCountryImage(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Name country, CancellationToken _) =>
                new Uri($"https://piu.test/flags/{country.ToString().ToLowerInvariant()}.png"));
        Services.AddSingleton(_users.Object);
    }

    private static User MakeUser(string name, string? country, string? gameTag = null) =>
        new(Guid.NewGuid(), Name.From(name), true, gameTag == null ? (Name?)null : Name.From(gameTag),
            new Uri("https://piu.test/a.png"), country == null ? (Name?)null : Name.From(country));

    [Fact]
    public void ReparameterizingFromFlaggedToCountrylessUserDropsTheWashAndDoesNotThrow()
    {
        this.RenderInteractive();
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("KR_PLAYER", "KR")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".user-label.has-flag")));

        // The dialog swaps its rows in place: same component, new user, no country.
        cut.SetParametersAndRender(p => p.Add(x => x.User, MakeUser("NOFLAG", null)));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".user-label.has-flag"));
            Assert.DoesNotContain("--label-flag", cut.Markup);
            Assert.Contains("NOFLAG", cut.Markup);
        });
    }

    [Fact]
    public void ReparameterizingAcrossCountriesSwapsTheWash()
    {
        this.RenderInteractive();
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("KR_PLAYER", "KR")));
        cut.WaitForAssertion(() =>
            Assert.Contains("--label-flag:url('https://piu.test/flags/kr.png')", cut.Markup));

        cut.SetParametersAndRender(p => p.Add(x => x.User, MakeUser("US_PLAYER", "US")));

        cut.WaitForAssertion(() =>
            Assert.Contains("--label-flag:url('https://piu.test/flags/us.png')", cut.Markup));
    }

    /// <summary>
    ///     The country used to be a 15px picture of a flag that cost the name ~26px of a phone's
    ///     Player column. It is a background now, so the country's NAME has to be somewhere a
    ///     reader can get at it: the tooltip it shares with the game tag.
    /// </summary>
    [Fact]
    public void TheCountryRidesTheNameTooltipRatherThanAPictureOfAFlag()
    {
        // The static path is the one that spells the tooltip into the markup as a title; in a
        // circuit the same string goes to MudTooltip, which renders it into a popover.
        SetRendererInfo(new RendererInfo("Static", false));
        var cut = RenderComponent<UserLabel>(p =>
            p.Add(x => x.User, MakeUser("KR_PLAYER", "South Korea", "TRICKFEET#1208")));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("TRICKFEET#1208 · South Korea",
                cut.Find(".user-label-name").GetAttribute("title"));
            // No flag image anywhere: the country is paint, not a picture.
            Assert.Empty(cut.FindAll("img"));
        });
    }

    [Fact]
    public void AnAccountWithNeitherTagNorCountryCarriesNoTooltipAtAll()
    {
        SetRendererInfo(new RendererInfo("Static", false));
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("PLAIN", null)));

        cut.WaitForAssertion(() => Assert.Null(cut.Find(".user-label-name").GetAttribute("title")));
    }

    /// <summary>
    ///     The boards that drew their own avatar hand it to the label so the wash covers both.
    ///     Size stays the caller's — the challenge boards run 20px, the widgets 24, the
    ///     community rankings a 26px square.
    /// </summary>
    [Fact]
    public void TheAvatarIsOptionalAndWearsTheCallersSize()
    {
        this.RenderInteractive();
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("KR_PLAYER", "KR")));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".user-label-avatar")));

        cut.SetParametersAndRender(p => p
            .Add(x => x.ShowAvatar, true)
            .Add(x => x.AvatarSize, 26)
            .Add(x => x.SquareAvatar, true));

        cut.WaitForAssertion(() =>
        {
            var avatar = cut.Find(".user-label-avatar");
            Assert.Contains("square", avatar.ClassName);
            Assert.Contains("--label-avatar-size:26px", avatar.GetAttribute("style"));
            Assert.Equal("https://piu.test/a.png", avatar.GetAttribute("src"));
        });
    }
}
