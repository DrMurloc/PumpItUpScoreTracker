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
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.Account;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one place the peer settings change — its own tab on /Account. Pinned: the catalog's sources render with their
///     counts, the tally is the union the reader would compute, PUMBILITY greys where it is empty,
///     and Save writes both settings under their keys.
/// </summary>
public sealed class PeersAndColorsPanelTests : ComponentTestBase
{
    private static readonly Guid Club = Guid.NewGuid();
    private static readonly Guid Region = Guid.NewGuid();
    private static readonly Guid Shared = Guid.NewGuid();
    private static readonly Guid RivalOnly = Guid.NewGuid();
    private static readonly Guid Clubmate = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public PeersAndColorsPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix);
        _mediator.Setup(m => m.Send(It.IsAny<GetPeerSourceCatalogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerSourceCatalog(new[]
            {
                new PeerSourceOption(PeerSourceKind.Rivals, null, "", false, false, true,
                    new HashSet<Guid> { Shared, RivalOnly }, new HashSet<Guid> { Shared, RivalOnly }, 1),
                new PeerSourceOption(PeerSourceKind.CompetitiveLevel, null, "", false, false, true,
                    new HashSet<Guid> { Shared, Guid.NewGuid(), Guid.NewGuid() }, new HashSet<Guid> { Guid.NewGuid() }, 0),
                new PeerSourceOption(PeerSourceKind.Pumbility, null, "", false, false, false,
                    new HashSet<Guid>(), new HashSet<Guid>(), 0),
                new PeerSourceOption(PeerSourceKind.Community, Club, "NorCal Pump", false, false, true,
                    new HashSet<Guid> { Clubmate, Shared }, new HashSet<Guid> { Clubmate, Shared }, 0),
                new PeerSourceOption(PeerSourceKind.Community, Region, "United States", true, false, true,
                    new HashSet<Guid> { Clubmate }, new HashSet<Guid> { Clubmate }, 0)
            }));
        // The preview's difficulty bubbles read scoring levels through the mediator this test
        // registers, so the base's stub for that query has to be repeated here.
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<Guid, double>)new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_settings.Object);
        this.RenderInteractive();
    }

    private IRenderedFragment RenderPanel()
    {
        return Render(builder =>
        {
            builder.OpenComponent<PeersAndColorsPanel>(0);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ListsEverySourceWithItsCountAndGreysAnEmptyOne()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("3 players · 1 board-only", cut.Markup);
            Assert.Contains("3 singles · 1 doubles", cut.Markup);
            Assert.Contains("Phoenix 2 only", cut.Markup);
            Assert.Contains("NorCal Pump", cut.Markup);
            Assert.Contains("United States", cut.Markup);
            Assert.Contains("pcd-opt-off", cut.Find("[data-testid='pcd-source-Pumbility']").ClassName);
        });
    }

    [Fact]
    public async Task TheTallyIsTheUnionOfWhatIsTicked()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        // The default ticks the competitive band alone: three singles.
        Assert.Contains("3", cut.Find("[data-testid='pcd-tally']").TextContent);

        await cut.Find("[data-testid='pcd-source-Rivals'] input").ChangeAsync(new ChangeEventArgs { Value = true });

        // Two rivals join, one of whom is already in the band: four, not five.
        cut.WaitForAssertion(() => Assert.Contains("4", cut.Find("[data-testid='pcd-tally']").TextContent));
    }

    [Fact]
    public async Task SaveWritesBothSettingsUnderTheirKeys()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        await cut.Find("[data-testid='pcd-source-Rivals'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        // MudRadio registers a click, not a change, on its input.
        await cut.Find("[data-testid='pcd-system-Podium'] input").ClickAsync(new MouseEventArgs());

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(PeerSourceSelection.SettingKey,
            It.Is<string>(v => v.Contains("Rivals") && v.Contains("Competitive")), It.IsAny<CancellationToken>()), Times.Once);
        _settings.Verify(s => s.SetSetting(ScoreColorSettings.SettingKey,
            It.Is<string>(v => v.Contains("system=Podium")), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Leaving a club leaves its id in the setting. The draft keeps only what the catalog can
    ///     offer, so the stale id neither ticks a phantom nor survives the next Save.
    /// </summary>
    [Fact]
    public async Task ACommunityYouLeftIsDroppedFromTheDraftAndFromSave()
    {
        var gone = Guid.NewGuid();
        _settings.Setup(s => s.GetSetting(PeerSourceSelection.SettingKey, default, null))
            .ReturnsAsync(new PeerSourceSelection(false, false, false, new HashSet<Guid> { Club, gone }).Serialize());
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(PeerSourceSelection.SettingKey,
            It.Is<string>(v => PeerSourceSelection.Parse(v).CommunityIds.SetEquals(new[] { Club })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ThePreviewPaintsSevenSamplesWithTheDraft()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() =>
        {
            var preview = cut.Find("[data-testid='pcd-preview']");
            Assert.Equal(7, preview.QuerySelectorAll("[data-testid='peer-score']").Length);
            Assert.Contains("#6 of 94 peers", preview.TextContent);
            Assert.Contains("PG · 12 of 88 peers have it", preview.TextContent);
        });
    }
}
