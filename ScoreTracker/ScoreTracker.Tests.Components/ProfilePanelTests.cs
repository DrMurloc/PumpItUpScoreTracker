using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.Web.Services;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.Account;
using ScoreTracker.Web.Services.Contracts;
using Xunit;



using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

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

        _uiSettings.Verify(u => u.ClearSetting("Culture", It.IsAny<CancellationToken>()), Times.Once);
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

    private MudSwitch<bool> Switch(IRenderedComponent<ProfilePanel> panel, string label)
    {
        return panel.FindComponents<MudSwitch<bool>>().Select(s => s.Instance).Single(s => s.Label == label);
    }

    /// <summary>
    ///     Hardmode is opt-in (hardmode-leaderboard.md D30): every account starts off, switching on
    ///     writes the setting, switching off clears it, and neither reloads the page.
    /// </summary>
    [Fact]
    public async Task HardmodeStartsOffAndItsSwitchWritesAndClearsTheOptIn()
    {
        var panel = RenderWithStoredLanguage(null);
        Assert.False(Switch(panel, "Hardmode").Value);

        await panel.InvokeAsync(() => Switch(panel, "Hardmode").ValueChanged.InvokeAsync(true));
        await panel.InvokeAsync(() => Switch(panel, "Hardmode").ValueChanged.InvokeAsync(false));

        _uiSettings.Verify(u => u.SetSetting(HardmodeOptIn.SettingKey, "true", It.IsAny<CancellationToken>()),
            Times.Once);
        _uiSettings.Verify(u => u.ClearSetting(HardmodeOptIn.SettingKey, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(NavigatedTo("/Account"));
    }

    private static readonly DateTimeOffset Recorded = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private async Task<BunitJSModuleInterop> DownloadScoresAsync(IRenderedComponent<ProfilePanel> panel)
    {
        var helpers = JSInterop.SetupModule("./js/helpers.js");
        helpers.SetupVoid("downloadFileFromStream", _ => true).SetVoidResult();
        await panel.FindAll("button").First(b => b.TextContent.Contains("Download Scores"))
            .ClickAsync(new MouseEventArgs());
        return helpers;
    }

    /// <summary>
    ///     The export is the file a player edits and uploads again, so every row has to read back in: the
    ///     score as plain digits and the plate as the mix's own code. It wrote "991,250" and "Ultimate Game",
    ///     and nearly every row failed on the way back.
    /// </summary>
    [Theory]
    [InlineData(MixEnum.Phoenix2)]
    [InlineData(MixEnum.Rise)]
    public async Task DownloadScoresHandsBackAFileTheUploadReads(MixEnum mix)
    {
        var passed = ScoreFileRoundTrip.CatalogChart(mix, "Conflict", ChartType.Single, 17);
        var broken = ScoreFileRoundTrip.CatalogChart(mix, "Switronic", ChartType.Single, 18);
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(mix);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { passed, broken });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(passed.Id, 991250, PhoenixPlate.UltimateGame, false, Recorded),
                new RecordedPhoenixScore(broken.Id, 702300, null, true, Recorded)
            });
        var panel = RenderWithStoredLanguage(null);

        var helpers = await DownloadScoresAsync(panel);

        var file = ScoreFileRoundTrip.DownloadedBytes(helpers.VerifyInvoke("downloadFileFromStream"));
        var (scores, errors) = await ScoreFileRoundTrip.ReadBack(file, mix, new[] { passed, broken });
        Assert.Empty(errors);
        var pass = Assert.Single(scores, s => s.ChartId == passed.Id);
        Assert.Equal(991250, (int)pass.Score!.Value);
        Assert.Equal(PhoenixPlate.UltimateGame, pass.Plate);
        Assert.True(Assert.Single(scores, s => s.ChartId == broken.Id).IsBroken);
    }

    /// <summary>Every mix before Phoenix keeps its records under its own name, and only XX's used to be read.</summary>
    [Fact]
    public async Task DownloadScoresOnAnOlderMixReadsThatMixsRecords()
    {
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Prime2);
        _mediator.Setup(m => m.Send(It.IsAny<GetXXBestChartAttemptsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BestXXChartAttempt>());
        var panel = RenderWithStoredLanguage(null);

        await DownloadScoresAsync(panel);

        _mediator.Verify(m => m.Send(It.Is<GetXXBestChartAttemptsQuery>(q => q.Mix == MixEnum.Prime2),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
