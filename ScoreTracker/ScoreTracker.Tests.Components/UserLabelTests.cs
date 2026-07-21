using Bunit;
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
///     when their type segment switches — so its flag must follow the CURRENT user, not the
///     first one it ever saw. The monthly dialog's type switch crashed on exactly this.
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
        this.RenderInteractive();
    }

    private static User MakeUser(string name, string? country) =>
        new(Guid.NewGuid(), Name.From(name), true, null, new Uri("https://piu.test/a.png"),
            country == null ? (Name?)null : Name.From(country));

    [Fact]
    public void ReparameterizingFromFlaggedToCountrylessUserDropsTheFlagAndDoesNotThrow()
    {
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("KR_PLAYER", "KR")));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("img")));

        // The dialog swaps its rows in place: same component, new user, no country.
        cut.SetParametersAndRender(p => p.Add(x => x.User, MakeUser("NOFLAG", null)));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("img"));
            Assert.Contains("NOFLAG", cut.Markup);
        });
    }

    [Fact]
    public void ReparameterizingAcrossCountriesSwapsTheFlag()
    {
        var cut = RenderComponent<UserLabel>(p => p.Add(x => x.User, MakeUser("KR_PLAYER", "KR")));
        cut.WaitForAssertion(() => Assert.Contains("flags/kr.png", cut.Markup));

        cut.SetParametersAndRender(p => p.Add(x => x.User, MakeUser("US_PLAYER", "US")));

        cut.WaitForAssertion(() => Assert.Contains("flags/us.png", cut.Markup));
    }
}
