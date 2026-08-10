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
using MudBlazor;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.Account;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The /Account language picker. The behaviour worth pinning is that the field can always
///     act on what it shows: it used to display the saved setting while the page rendered
///     something else, and MudSelect raises nothing for an unchanged value, so re-picking the
///     language you were already looking at did nothing at all
///     (docs/design/culture-resolution.md).
/// </summary>
public sealed class ProfilePanelTests : ComponentTestBase
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private readonly Mock<IUserRepository> _users = new();

    public ProfilePanelTests()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(UserId, Name.From("Jordan Alvarez"), true, null,
            new Uri("https://example.invalid/a.png"), null));

        _users.Setup(u => u.GetCountries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CountryRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyToolConnectionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerToolConnectionRecord>());

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(_users.Object);
    }

    private IRenderedComponent<ProfilePanel> RenderWithStoredLanguage(string? culture)
    {
        _uiSettings.Setup(u => u.GetSetting("Culture", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(culture);
        return RenderComponent<ProfilePanel>();
    }

    private static MudSelect<string> LanguageField(IRenderedComponent<ProfilePanel> panel)
    {
        return panel.FindComponents<MudSelect<string>>()
            .Select(f => f.Instance)
            .Single(f => f.Label == "Language");
    }

    private Task ChooseAsync(IRenderedComponent<ProfilePanel> panel, string value)
    {
        return panel.InvokeAsync(() => LanguageField(panel).ValueChanged.InvokeAsync(value));
    }

    private bool NavigatedTo(string fragment)
    {
        return Services.GetRequiredService<FakeNavigationManager>().History
            .Any(h => h.Uri.Contains(fragment, StringComparison.Ordinal));
    }

    /// <summary>
    ///     A player who has never chosen sits on Automatic — which is what they are already
    ///     getting. Preselecting their browser's language instead would leave the field unable to
    ///     act: picking the entry already selected raises nothing, so the setting never gets
    ///     written and the account keeps following whatever browser it is opened in.
    /// </summary>
    [Fact]
    public void ShowsAutomaticWhenNoLanguageHasEverBeenChosen()
    {
        var panel = RenderWithStoredLanguage(null);

        Assert.Equal(SupportedCultures.Automatic, LanguageField(panel).Value);
    }

    [Fact]
    public void ShowsTheSavedLanguage()
    {
        var panel = RenderWithStoredLanguage("ja-JP");

        Assert.Equal("ja-JP", LanguageField(panel).Value);
    }

    /// <summary>
    ///     A row written before the es → es-ES split still reads "es", which matches no entry —
    ///     the field would render blank and the player would think nothing was set.
    /// </summary>
    [Fact]
    public void ResolvesALegacyStoredCodeToTheEntryThatRepresentsIt()
    {
        var panel = RenderWithStoredLanguage("es");

        Assert.Equal("es-ES", LanguageField(panel).Value);
    }

    [Fact]
    public async Task ChoosingALanguageSavesItAndReloadsThroughTheCultureEndpoint()
    {
        var panel = RenderWithStoredLanguage(null);

        await ChooseAsync(panel, "ko-KR");

        _uiSettings.Verify(u => u.SetSetting("Culture", "ko-KR", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(NavigatedTo("/Culture/Set?culture=ko-KR"));
    }

    /// <summary>
    ///     Automatic is stored as absence, so it clears rather than writes — and it has to drop
    ///     the cookie too, or the cached choice answers in place of the browser.
    /// </summary>
    [Fact]
    public async Task ChoosingAutomaticClearsTheSettingAndTheCookie()
    {
        var panel = RenderWithStoredLanguage("ja-JP");

        await ChooseAsync(panel, SupportedCultures.Automatic);

        _mediator.Verify(m => m.Send(It.Is<ClearUserUiSettingCommand>(c => c.SettingName == "Culture"),
            It.IsAny<CancellationToken>()), Times.Once);
        _uiSettings.Verify(u => u.SetSetting("Culture", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.True(NavigatedTo("/Culture/Clear"));
    }

    /// <summary>The sentinel is a picker value — it must never reach the culture endpoint as one.</summary>
    [Fact]
    public async Task NeverSendsTheSentinelToTheCultureEndpoint()
    {
        var panel = RenderWithStoredLanguage("ja-JP");

        await ChooseAsync(panel, SupportedCultures.Automatic);

        Assert.False(NavigatedTo("/Culture/Set"));
    }
}
