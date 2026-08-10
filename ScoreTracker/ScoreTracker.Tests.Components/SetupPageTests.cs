using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The new-account setup step (docs/design/new-user-setup.md). The behaviour worth pinning
///     is that <em>every field writes through on change</em> — Continue is a hand-off, not a
///     save — because that is what makes the language reload safe and what a future edit is
///     most likely to quietly undo.
/// </summary>
public sealed class SetupPageTests : ComponentTestBase
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private readonly Mock<IUserRepository> _users = new();

    public SetupPageTests()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(UserId, Name.From("Jordan Alvarez"), false, null,
            new Uri("https://example.invalid/a.png"), null));

        _users.Setup(u => u.GetCountries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CountryRecord(Name.From("Chile"), new Uri("https://example.invalid/cl.png")),
                new CountryRecord(Name.From("Peru"), new Uri("https://example.invalid/pe.png"))
            });

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(_users.Object);
    }

    private IRenderedComponent<Setup> Render() => RenderComponent<Setup>();

    /// <summary>
    ///     Puts a game tag on the account and answers the doorway lookup with
    ///     <paramref name="tagIsAlsoCarriedBy" /> — the accounts a self-reported tag collides with.
    /// </summary>
    private IRenderedComponent<Setup> RenderCarryingGameTag(string tag,
        params User[] tagIsAlsoCarriedBy)
    {
        CurrentUser.Setup(c => c.User).Returns(new User(UserId, Name.From("Jordan Alvarez"), false,
            Name.From(tag), new Uri("https://example.invalid/a.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetUsersByGameTagQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tagIsAlsoCarriedBy);
        return Render();
    }

    private static User AccountNamed(string name) =>
        new(Guid.NewGuid(), Name.From(name), true, Name.From(name),
            new Uri("https://example.invalid/b.png"), null);

    /// <summary>
    ///     [SupplyParameterFromQuery] only binds from the address, so the provider has to arrive
    ///     the way it really does — on the URL — rather than as a parameter.
    /// </summary>
    private IRenderedComponent<Setup> RenderArrivingFrom(string from)
    {
        Services.GetRequiredService<FakeNavigationManager>()
            .NavigateTo($"/Setup?from={Uri.EscapeDataString(from)}");
        return RenderComponent<Setup>();
    }

    private int NavigationCount => Services.GetRequiredService<FakeNavigationManager>().History.Count;

    /// <summary>True when any navigation this test caused targeted <paramref name="fragment" />.</summary>
    private bool NavigatedTo(string fragment) =>
        Services.GetRequiredService<FakeNavigationManager>().History
            .Any(h => h.Uri.Contains(fragment, StringComparison.Ordinal));

    private string Navigations =>
        string.Join(" | ", Services.GetRequiredService<FakeNavigationManager>().History.Select(h => h.Uri));

    /// <summary>
    ///     The username arrives prefilled from whatever the provider gave, which is the whole
    ///     reason this page exists — Google and Facebook hand over a real name.
    /// </summary>
    [Fact]
    public void PrefillsTheUsernameFromTheAccount()
    {
        var page = Render();

        Assert.Equal("Jordan Alvarez", page.Find("#setup-username").GetAttribute("value"));
    }

    /// <summary>
    ///     A prefilled field reads as already handled; the same field labelled "filled in from
    ///     Google" reads as needing a decision. The provider arrives on the query string, so
    ///     only known ones may render — anything else shows no chip rather than echoing it.
    /// </summary>
    [Theory]
    [InlineData("Google", "filled in from Google")]
    [InlineData("Discord", "filled in from Discord")]
    [InlineData("PiuGame", "filled in from PIUGAME")]
    public void NamesTheProviderThatFilledTheUsernameIn(string from, string expected)
    {
        var page = RenderArrivingFrom(from);

        Assert.Contains(expected, page.Markup);
    }

    [Theory]
    [InlineData("Myspace")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("")]
    public void ShowsNoProviderChipForAnythingUnrecognised(string from)
    {
        var page = RenderArrivingFrom(from);

        Assert.DoesNotContain("filled in from", page.Markup);
        Assert.DoesNotContain("Myspace", page.Markup);
        Assert.DoesNotContain("<script>alert", page.Markup);
    }

    /// <summary>Phoenix 2 is the opening choice — GetSelectedMix answers Phoenix for "unset" (D5).</summary>
    [Fact]
    public void PreselectsPhoenix2ForAnAccountThatHasNeverChosenAMix()
    {
        var page = Render();

        var pressed = page.FindAll("button.setup-gbtn")
            .Single(b => b.GetAttribute("aria-pressed") == "true");
        Assert.Equal("Phoenix 2", pressed.TextContent.Trim());
    }

    [Fact]
    public void KeepsAMixTheAccountAlreadyChose()
    {
        _uiSettings.Setup(u => u.GetSetting(IUiSettingsAccessor.MixSettingKey, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(MixEnum.XX.ToString());

        var pressed = Render().FindAll("button.setup-gbtn")
            .Single(b => b.GetAttribute("aria-pressed") == "true");
        Assert.Equal("XX", pressed.TextContent.Trim());
    }

    /// <summary>Public is off on arrival and stays a decision the player makes (D4).</summary>
    [Fact]
    public void StartsPrivate()
    {
        var page = Render();

        Assert.Equal("false", page.Find("button.setup-switch").GetAttribute("aria-checked"));
    }

    [Fact]
    public async Task SavesTheUsernameOnChange()
    {
        var page = Render();

        await page.Find("#setup-username").ChangeAsync(new() { Value = "  MURLOCSLAYER  " });

        _mediator.Verify(m => m.Send(
            It.Is<UpdateUserCommand>(c => c.newName == Name.From("MURLOCSLAYER")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     There is no submit step to validate against, so an unusable value is refused at the
    ///     field and the last good one goes back — never persisted, never left in place.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnEmptyUsernameAndRestoresTheLastGoodOne(string entered)
    {
        var page = Render();

        await page.Find("#setup-username").ChangeAsync(new() { Value = entered });

        _mediator.Verify(m => m.Send(It.IsAny<UpdateUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal("Jordan Alvarez", page.Find("#setup-username").GetAttribute("value"));
    }

    [Fact]
    public async Task SavesTheCountryOnChange()
    {
        var page = Render();

        await page.Find("#setup-country").ChangeAsync(new() { Value = "Peru" });

        _mediator.Verify(m => m.Send(
            It.Is<UpdateUserCommand>(c => c.newCountry == Name.From("Peru")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Clearing sends null rather than an empty Name, which would not construct.</summary>
    [Fact]
    public async Task ClearingTheCountrySendsNull()
    {
        CurrentUser.Setup(c => c.User).Returns(new User(UserId, Name.From("Jordan Alvarez"), false, null,
            new Uri("https://example.invalid/a.png"), Name.From("Chile")));
        var page = Render();

        await page.Find("#setup-country").ChangeAsync(new() { Value = "" });

        _mediator.Verify(m => m.Send(
            It.Is<UpdateUserCommand>(c => c.newCountry == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TogglingPublicSavesImmediately()
    {
        var page = Render();

        await page.Find("button.setup-switch").ClickAsync(new());

        _mediator.Verify(m => m.Send(
            It.Is<UpdateUserCommand>(c => c.newIsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("true", page.Find("button.setup-switch").GetAttribute("aria-checked"));
    }

    /// <summary>
    ///     The mix persists on click so abandoning the page keeps the choice, and the page
    ///     re-emits the token block rather than reloading (D9/D10).
    /// </summary>
    [Fact]
    public async Task PickingAMixPersistsItAndRepaintsTheTokensWithoutNavigating()
    {
        var page = Render();
        var before = NavigationCount;

        await page.FindAll("button.setup-gbtn")
            .Single(b => b.TextContent.Trim() == "XX")
            .ClickAsync(new());

        _uiSettings.Verify(u => u.SetSelectedMix(MixEnum.XX, It.IsAny<CancellationToken>()), Times.Once);
        // XX's primary, straight out of the palette the page re-emitted.
        Assert.Contains("--mix-primary: #FF2FA0", page.Markup);
        Assert.DoesNotContain("--mix-primary: #4FE33F", page.Markup);
        Assert.Equal(before, NavigationCount);
    }

    /// <summary>
    ///     The game-tag doorway asks on the way past instead of intercepting the sign-in
    ///     (docs/design/login-overhaul-spec.md C6), and hands the wizard both the account to
    ///     compare against and the way back to setup.
    /// </summary>
    [Fact]
    public void AGameTagAnotherAccountCarriesOffersTheMerge()
    {
        var other = AccountNamed("Cassandra Vex");

        var page = RenderCarryingGameTag("ERRLENA", other);

        var href = page.Find(".setup-merge-link").GetAttribute("href")!;
        Assert.Contains($"with={other.Id}", href);
        Assert.Contains("returnUrl=%2FSetup", href);
    }

    /// <summary>
    ///     …and never names what it matched. A game tag is self-reported and non-unique, so the
    ///     invitation reaches strangers who share a nickname; naming the account would tell them
    ///     it exists and who it belongs to.
    /// </summary>
    [Fact]
    public void TheInvitationNeverNamesTheMatchedAccount()
    {
        var page = RenderCarryingGameTag("ERRLENA", AccountNamed("Cassandra Vex"));

        Assert.Contains("ERRLENA", page.Markup);
        Assert.DoesNotContain("Cassandra Vex", page.Markup);
    }

    [Fact]
    public void AGameTagNobodyElseCarriesAsksNothing()
    {
        var page = RenderCarryingGameTag("ERRLENA");

        Assert.Empty(page.FindAll(".setup-merge"));
    }

    /// <summary>
    ///     An OAuth sign-in creates an account with no game tag, so there is nothing to collide
    ///     with and the lookup never runs.
    /// </summary>
    [Fact]
    public void AnAccountWithNoGameTagIsNeverAsked()
    {
        var page = Render();

        Assert.Empty(page.FindAll(".setup-merge"));
        _mediator.Verify(m => m.Send(It.IsAny<GetUsersByGameTagQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Sharing follows privacy here as it does on /Account. A public profile with no share
    ///     preference reads as "not shared", so an account that opens up during setup would
    ///     otherwise sit outside the pool every other public account belongs to.
    /// </summary>
    [Fact]
    public async Task OpeningTheProfileGrantsTheAllToolsShare()
    {
        var page = Render();

        await page.Find("button.setup-switch").ClickAsync(new());

        _mediator.Verify(m => m.Send(It.Is<SetShareWithAllToolsCommand>(c => c.Share),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>…and closing it again withdraws the grant, so the two never disagree.</summary>
    [Fact]
    public async Task ClosingTheProfileWithdrawsTheAllToolsShare()
    {
        CurrentUser.Setup(c => c.User).Returns(new User(UserId, Name.From("Jordan Alvarez"), true, null,
            new Uri("https://example.invalid/a.png"), null));
        var page = Render();

        await page.Find("button.setup-switch").ClickAsync(new());

        _mediator.Verify(m => m.Send(It.Is<SetShareWithAllToolsCommand>(c => !c.Share),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Continue is a hand-off: it marks setup done and routes through /Mix/Set so the
    ///     anonymous-fallback cookie — which a circuit cannot write — lands on the way home.
    /// </summary>
    [Fact]
    public async Task ContinueMarksSetupDoneAndHandsOffThroughMixSet()
    {
        var page = Render();

        await page.Find("button.setup-continue").ClickAsync(new());

        _uiSettings.Verify(u => u.SetSetting(IUiSettingsAccessor.SetupCompletedSettingKey, "true", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(NavigatedTo("/Mix/Set?mix=Phoenix2"), Navigations);
    }

    /// <summary>
    ///     Language cannot change inside a live circuit, so it is a real navigation — and the
    ///     provider carries through, or the username chip would vanish on the way back (D8).
    /// </summary>
    [Fact]
    public async Task ChangingLanguageNavigatesThroughCultureSetAndComesBackToSetup()
    {
        var page = RenderArrivingFrom("Google");

        await page.Find("#setup-language").ChangeAsync(new() { Value = "ko-KR" });

        _uiSettings.Verify(u => u.SetSetting("Culture", "ko-KR", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(NavigatedTo("/Culture/Set?culture=ko-KR"), Navigations);
        Assert.True(NavigatedTo(Uri.EscapeDataString("/Setup?from=Google")), Navigations);
    }

    /// <summary>An unsupported culture is not persisted and does not navigate.</summary>
    [Fact]
    public async Task IgnoresAnUnsupportedLanguage()
    {
        var page = Render();
        var before = NavigationCount;

        await page.Find("#setup-language").ChangeAsync(new() { Value = "zz-ZZ" });

        _uiSettings.Verify(u => u.SetSetting("Culture", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(before, NavigationCount);
    }

    [Fact]
    public void SendsAnAnonymousVisitorToTheFrontDoor()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);

        Render();

        Assert.True(NavigatedTo("/Login"), Navigations);
    }
}
