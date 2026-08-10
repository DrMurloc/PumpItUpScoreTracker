using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The refreshed /UploadPhoenixScores page (docs/design/import-scores-refresh.md): the
///     password form IS the page, the script/CSV flow sits collapsed in the manual-import
///     expander, and Import registers on the page dock. The wire-to-ledger path stays E2E's.
/// </summary>
public sealed class UploadPhoenixScoresPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private readonly Mock<IImportCredentialClientStore> _clientStore = new();
    private readonly Mock<IUiNotificationHub> _uiHub = new();

    public UploadPhoenixScoresPageTests()
    {
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MixEnum.Phoenix);
        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        _uiSettings.Setup(u => u.SetSetting(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_uiSettings.Object);

        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeChart() });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoreRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ScoreRankingRecord>());
        // Empty, not absent: a player who has never imported gets no strip, which is the state
        // these tests are all about. The handler never returns null in production.
        _mediator.Setup(m => m.Send(It.IsAny<GetImportHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ImportAttemptRecord>());
        Services.AddSingleton(_mediator.Object);
        Services.AddScoped<ChartScoringLevels>();

        // No stored credential unless a test says otherwise.
        _clientStore.Setup(s => s.Read(It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoredCredentialBlob?)null);
        Services.AddSingleton(_clientStore.Object);

        _uiHub.Setup(h => h.Subscribe(It.IsAny<string>(), It.IsAny<Func<ImportStatusUpdatedEvent, Task>>()))
            .Returns(Mock.Of<IDisposable>());
        _uiHub.Setup(h => h.Subscribe(It.IsAny<string>(), It.IsAny<Func<ImportStatusErrorEvent, Task>>()))
            .Returns(Mock.Of<IDisposable>());
        Services.AddSingleton(_uiHub.Object);

        Services.AddSingleton(Mock.Of<IPhoenixScoreFileExtractor>());
        Services.AddScoped<PageDockService>();
        Services.AddLogging();

        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/avatar.png"), null));
    }

    private static Chart MakeChart() =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song("District 1", SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Doin", Bpm.From(195, 195)),
            ChartType.Single, 20, MixEnum.Phoenix, null, 1200, new HashSet<Skill>());

    private void StoreCredential() =>
        _clientStore.Setup(s => s.Read(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredCredentialBlob(Guid.NewGuid(), "sealed", 0));

    private IReadOnlyList<AngleSharp.Dom.IElement> ImportButtons(IRenderedComponent<UploadPhoenixScores> cut) =>
        cut.FindAll("button").Where(b => b.TextContent.Trim() == "Import").ToArray();

    [Fact]
    public void TheFormIsThePage()
    {
        var cut = RenderComponent<UploadPhoenixScores>();

        // Credentials render immediately — no mode step in front of them.
        Assert.Contains(cut.FindAll("input"), i => i.GetAttribute("type") == "password");
        Assert.Contains("Import Lede", cut.Markup);
        // The old four-paragraph methodology is gone.
        Assert.DoesNotContain("Phoenix Import Info", cut.Markup);
        // The desktop Import seat renders in the card footer (the dock copy renders through
        // MainLayout's slot, which isn't part of this tree) and is disabled until credentials exist.
        var imports = ImportButtons(cut);
        Assert.Equal(1, imports.Count);
        Assert.All(imports, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void SavedCredentialSwapsOnlyTheCredentialFields()
    {
        StoreCredential();

        var cut = RenderComponent<UploadPhoenixScores>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved on this device", cut.Markup);
            // The credential fields disappear — and so does the Remember checkbox, since the
            // saved alert already says it; the other options and Import stay.
            Assert.DoesNotContain(cut.FindAll("input"), i => i.GetAttribute("type") == "password");
            Assert.DoesNotContain("Remember my password on this device", cut.Markup);
            Assert.Contains("Record broken scores as your best", cut.Markup);
            Assert.All(ImportButtons(cut), b => Assert.False(b.HasAttribute("disabled")));
        });
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, false)]
    [InlineData(MixEnum.Phoenix2, true)]
    public void RecordBrokenAsBestDefaultsOnOnlyForPhoenix2(MixEnum mix, bool expectedChecked)
    {
        // Phoenix 2's best-scores list carries broken attempts with real partial scores, so
        // including them is that mix's default; Phoenix keeps the opt-in. The box stays
        // user-overridable either way.
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(mix);

        var cut = RenderComponent<UploadPhoenixScores>();

        var label = cut.FindAll("label").First(l => l.TextContent.Contains("Record broken scores as your best"));
        var input = label.QuerySelector("input");
        Assert.NotNull(input);
        Assert.Equal(expectedChecked, input!.HasAttribute("checked"));
    }

    [Fact]
    public void ManualImportIsCollapsedByDefault()
    {
        var cut = RenderComponent<UploadPhoenixScores>();

        Assert.Contains("Manual import — console script + CSV", cut.Markup);
        Assert.Empty(cut.FindAll(".mud-expand-panel.mud-panel-expanded"));
    }

    [Fact]
    public void ImportRegistersOnThePageDock()
    {
        var cut = RenderComponent<UploadPhoenixScores>();

        var dock = Services.GetRequiredService<PageDockService>();
        Assert.NotNull(dock.DockContent);
        Assert.False(dock.FocusMode);
    }

    [Fact]
    public void StartingAnImportDisablesTheFormAndShapesTheIncomingResults()
    {
        StoreCredential();
        _mediator.Setup(m => m.Send(It.IsAny<StartOfficialImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportStartResult(ImportStartOutcome.Started));

        var cut = RenderComponent<UploadPhoenixScores>();
        cut.WaitForAssertion(() => Assert.All(ImportButtons(cut), b => Assert.False(b.HasAttribute("disabled"))));

        ImportButtons(cut).First().Click();

        cut.WaitForAssertion(() =>
        {
            // The import runs in the background: the page says so, the form locks, and the
            // results area renders shaped skeletons instead of a bare status line.
            Assert.Contains("You can leave this page.", cut.Markup);
            Assert.All(ImportButtons(cut), b => Assert.True(b.HasAttribute("disabled")));
            Assert.NotEmpty(cut.FindAll(".mud-skeleton"));
        });
        _mediator.Verify(m => m.Send(It.IsAny<StartOfficialImportCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(MixEnum.Phoenix)]
    [InlineData(MixEnum.Phoenix2)]
    public async Task ChangeCardListsTheSelectedMixesCards(MixEnum mix)
    {
        // Each official site lists only the cards registered in its own game, so asking Phoenix 1
        // for a Phoenix 2 player's cards returns their old card and nothing else — which is what
        // "Change Card only shows my first card" looked like from a Phoenix 2 import.
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(mix);
        // A remembered card with no loaded list is the state that offers the button.
        _uiSettings.Setup(u => u.GetSetting("PhoenixScoreUpload__LastGameId", It.IsAny<CancellationToken>(),
            It.IsAny<Guid?>())).ReturnsAsync("9990001");
        _uiSettings.Setup(u => u.GetSetting("PhoenixScoreUpload__LastGameTag", It.IsAny<CancellationToken>(),
            It.IsAny<Guid?>())).ReturnsAsync("OLDCARD");
        _mediator.Setup(m => m.Send(It.IsAny<GetGameCardsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new GameCardRecord("NEWCARD", "9990002", true) });

        var cut = RenderComponent<UploadPhoenixScores>();
        cut.WaitForAssertion(() => Assert.Contains("Change Card", cut.Markup));
        // The button gates on typed credentials. MudTextField binds on change, not input.
        await cut.Find("input[type=text]").ChangeAsync(new ChangeEventArgs { Value = "player" });
        await cut.Find("input[type=password]").ChangeAsync(new ChangeEventArgs { Value = "hunter2" });

        await cut.FindAll("button").First(b => b.TextContent.Contains("Change Card"))
            .ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<GetGameCardsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
