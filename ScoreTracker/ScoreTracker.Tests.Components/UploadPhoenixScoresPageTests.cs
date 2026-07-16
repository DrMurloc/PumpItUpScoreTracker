using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
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
            // The credential fields disappear; the options and Import stay.
            Assert.DoesNotContain(cut.FindAll("input"), i => i.GetAttribute("type") == "password");
            Assert.Contains("Include Broken Scores", cut.Markup);
            Assert.All(ImportButtons(cut), b => Assert.False(b.HasAttribute("disabled")));
        });
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
}
