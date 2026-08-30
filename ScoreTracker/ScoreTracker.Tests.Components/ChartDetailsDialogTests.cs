using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The shared quick-look dialog. These pin the Report Video affordance: it lives with
///     the video it reports — no video, no report — and a click names the chart to an
///     admin, so a wrong video gets found by whoever is watching it.
/// </summary>
public sealed class ChartDetailsDialogTests : TestContext
{
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAdminNotificationClient> _notifications = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public ChartDetailsDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_notifications.Object);
        Services.AddSingleton(_currentUser.Object);
        // The tab the dialog opens on is remembered per user; nothing stored means the default.
        Services.AddSingleton(_uiSettings.Object);
        _uiSettings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        _currentUser.Setup(u => u.IsLoggedIn).Returns(false);
        // The bubble nested in the title row injects this and reads through it.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddScoped<ChartScoringLevels>();
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartIdentityRecord>());
        // The dialog hosts the shared leaderboard now, which resolves the user reader to put
        // names and avatars on its rows.
        Services.AddSingleton(Mock.Of<IUserReader>());
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        // That board also asks whether the chart carries a limbo leaderboard. Left unstubbed the
        // mock hands back null and the component dereferences it during load — which fails every
        // test in this file, none of which is about leaderboards.
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ScoreLedger.Contracts.Queries.GetLimboChartsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<Guid>)new HashSet<Guid>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        // The similarity graph is empty on a fresh database and until the nightly rebuild has
        // run once, so that is the default here too — the drill-down test seeds its own.
        _mediator.Setup(m => m.Send(It.IsAny<GetSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartSimilarityRecord>());
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
        // Last: it reads the renderer, locking the service collection. The dialog renders on
        // its interactive path (RendererInfo gates the nested bubble's tooltip).
        this.RenderInteractive();
    }

    private Chart SetupChart(string? videoUrl)
    {
        var chart = ChartSlugsTests.BuildChart(song: "Anchor");
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChartVideoInformation>)(videoUrl == null
                ? Array.Empty<ChartVideoInformation>()
                : new[] { new ChartVideoInformation(chart.Id, new Uri(videoUrl), Name.From("Some Channel")) }));
        return chart;
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment RenderDialog(Chart chart, ChartDetailsDialog.DetailsTab? initialTab = null,
        EventCallback<Guid>? onToDo = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartDetailsDialog>(1);
            builder.AddAttribute(2, nameof(ChartDetailsDialog.Chart), chart);
            builder.AddAttribute(3, nameof(ChartDetailsDialog.Visible), true);
            builder.AddAttribute(4, nameof(ChartDetailsDialog.InitialTab), initialTab);
            if (onToDo != null) builder.AddAttribute(5, nameof(ChartDetailsDialog.OnToDo), onToDo.Value);
            builder.CloseComponent();
        });
    }

    /// <summary>
    ///     The video leads the dialog and is the one thing outside the tabs, so a chart with
    ///     one shows it above the title regardless of which tab is open.
    /// </summary>
    [Fact]
    public void AChartWithAVideoLeadsWithIt()
    {
        var cut = RenderDialog(SetupChart("https://www.youtube.com/embed/abc"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("iframe.chart-details-video")));
    }

    // --- The singles video side caption (docs/design/video-sides.md) ---

    /// <summary>A Single 22 whose stored side is Right, paired with a Single 17 on the mix.</summary>
    private Chart SetupPairedChart(bool partnerInMix)
    {
        var chart = ChartSlugsTests.BuildChart(song: "Uh-Heung",
            type: SharedKernel.Enums.ChartType.Single, level: 22);
        var partner = ChartSlugsTests.BuildChart(song: "Uh-Heung",
            type: SharedKernel.Enums.ChartType.Single, level: 17);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChartVideoInformation>)new[]
            {
                new ChartVideoInformation(chart.Id, new Uri("https://www.youtube.com/embed/abc"),
                    Name.From("NEVSISTER"), VideoSide.Right, partner.Id)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Chart>)(partnerInMix ? new[] { partner } : Array.Empty<Chart>()));
        return chart;
    }

    [Fact]
    public void ASharedVideoCaptionsTheSidesWithTheViewedChartEmphasized()
    {
        var cut = RenderDialog(SetupPairedChart(partnerInMix: true));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".video-side-caption")));
        Assert.Contains("S22", cut.Find(".video-side-on").TextContent);
        Assert.Contains("S17", cut.Find(".video-side-caption").TextContent);
    }

    [Fact]
    public void ASoloVideoShowsNoSideCaption()
    {
        var cut = RenderDialog(SetupChart("https://www.youtube.com/embed/abc"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("iframe.chart-details-video")));
        Assert.Empty(cut.FindAll(".video-side-caption"));
    }

    [Fact]
    public void APartnerAbsentFromTheSelectedMixCaptionsTheViewedHalfAlone()
    {
        var cut = RenderDialog(SetupPairedChart(partnerInMix: false));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".video-side-caption")));
        Assert.Contains("S22", cut.Find(".video-side-on").TextContent);
        Assert.DoesNotContain("S17", cut.Find(".video-side-caption").TextContent);
    }

    [Fact]
    public void ReopeningTheSameChartUnderAnotherMixReloadsThePartner()
    {
        // The partner's caption label carries the selected mix's level, so the id-only reload
        // guard would show mix A's partner under mix B (the randomizer surfaces pass a
        // per-chart mix).
        var chart = SetupPairedChart(partnerInMix: true);
        var partnerMixes = new List<MixEnum>();
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<IEnumerable<Chart>> q, CancellationToken _) =>
                partnerMixes.Add(((GetChartsQuery)q).Mix))
            .ReturnsAsync(Array.Empty<Chart>());
        var cut = RenderDialog(chart);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".video-side-caption")));
        var dialog = cut.FindComponent<ChartDetailsDialog>();

        dialog.SetParametersAndRender(p => p.Add(c => c.Visible, false));
        dialog.SetParametersAndRender(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.Mix, MixEnum.Phoenix2));

        cut.WaitForAssertion(() => Assert.Contains(MixEnum.Phoenix2, partnerMixes));
    }

    [Fact]
    public void AChartWithNoVideoRendersTheRestOfTheDialog()
    {
        var cut = RenderDialog(SetupChart(null), ChartDetailsDialog.DetailsTab.Stats);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".chart-details-meta")));
        Assert.Empty(cut.FindAll("iframe.chart-details-video"));
    }

    /// <summary>
    ///     Ten of the thirteen hosts never wire OnToDo, and every one of them used to render a
    ///     bookmark whose click invoked an unbound callback. A control that does nothing is
    ///     worse than an absent one, so the affordance follows the delegate.
    /// </summary>
    [Fact]
    public void TheToDoBookmarkIsAbsentWhereNoHostHandlesIt()
    {
        SignIn();
        var cut = RenderDialog(SetupChart(null));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".chart-details-title")));
        Assert.Empty(cut.FindAll("[aria-label='To Do']"));
    }

    [Fact]
    public void TheToDoBookmarkAppearsWhereAHostHandlesIt()
    {
        SignIn();
        var cut = RenderDialog(SetupChart(null), onToDo: EventCallback.Factory.Create<Guid>(this, _ => { }));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[aria-label='To Do']")));
    }

    /// <summary>The board reads CurrentUser.User, so IsLoggedIn alone is a half-built user.</summary>
    private void SignIn()
    {
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User)
            .Returns(new User(Guid.NewGuid(), "Me", true, null, new Uri("https://piu.test/me.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
    }

    /// <summary>
    ///     The dialog hosts the shared board rather than growing one of its own. Asserting on
    ///     the scope rail is what distinguishes the two — a private copy would have no scopes.
    ///     It is also the default tab, so this doubles as the check that the dialog opens on it.
    /// </summary>
    [Fact]
    public void TheSharedBoardRendersInsideTheDialogAndIsTheDefaultTab()
    {
        var cut = RenderDialog(SetupChart(null));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='cld-scope-World']"));
            Assert.Equal("true",
                cut.Find("[data-testid='cdt-tab-Leaderboard']").GetAttribute("aria-selected"));
        });
    }

    /// <summary>
    ///     The point of the tabs. A panel nobody selected must not fetch: the stats panel's
    ///     identity chips are the cheapest thing to assert on, because the meta grid renders from
    ///     the Chart itself and would be present either way.
    /// </summary>
    [Fact]
    public void AnUnselectedTabFetchesNothing()
    {
        RenderDialog(SetupChart(null));

        _mediator.Verify(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     A caller that opens this dialog for one section says so, and is obeyed — which is
    ///     what keeps recording a score one tap away from the widgets that exist to record.
    /// </summary>
    [Fact]
    public void InitialTabDecidesWhereTheDialogOpens()
    {
        var cut = RenderDialog(SetupChart(null), ChartDetailsDialog.DetailsTab.Stats);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("[data-testid='cdt-tab-Stats']").GetAttribute("aria-selected"));
            _mediator.Verify(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    /// <summary>
    ///     Score History is your journal plus your recording inputs. Signed out both are empty,
    ///     so the tab is not offered at all rather than opening onto nothing.
    /// </summary>
    [Fact]
    public void SignedOutThereIsNoScoreHistoryTab()
    {
        var cut = RenderDialog(SetupChart(null));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='cdt-tab-Stats']")));
        Assert.Empty(cut.FindAll("[data-testid='cdt-tab-History']"));
    }

    /// <summary>
    ///     Comments ship behind ChartComments:Enabled, which is off in configuration until
    ///     production cost testing says otherwise. No options registered means the default —
    ///     disabled — which is the state everyone but the owner sees on the day this merges.
    /// </summary>
    [Fact]
    public void TheCommentsTabIsGatedOffByDefault()
    {
        var cut = RenderDialog(SetupChart(null));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='cdt-tab-Leaderboard']")));
        Assert.Empty(cut.FindAll("[data-testid='cdt-tab-Comments']"));
    }

    /// <summary>
    ///     ...and the site admin sees it anyway, which is what makes the gated period useful
    ///     rather than merely quiet. User.IsAdmin is a computed Guid, so this needs no flag.
    /// </summary>
    [Fact]
    public void TheSiteAdminSeesCommentsWithTheGateStillClosed()
    {
        _currentUser.Setup(u => u.IsLoggedIn).Returns(true);
        _currentUser.Setup(u => u.User).Returns(new User(
            Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"), Name.From("DrMurloc"), true, null,
            new Uri("https://example.com/d.png"), Name.From("US")));

        var cut = RenderDialog(SetupChart(null));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='cdt-tab-Comments']")));
    }

    /// <summary>
    ///     Ten call sites render this dialog, so a closed one must cost nothing. Active follows
    ///     Visible; with it false the board must not reach for a board nobody asked to see.
    /// </summary>
    [Fact]
    public void AClosedDialogAsksForNoBoard()
    {
        var chart = SetupChart(null);

        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartDetailsDialog>(1);
            builder.AddAttribute(2, nameof(ChartDetailsDialog.Chart), chart);
            builder.AddAttribute(3, nameof(ChartDetailsDialog.Visible), false);
            builder.CloseComponent();
        });

        _mediator.Verify(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Tapping a similar chart swaps the dialog in place and offers the way back. The host
    ///     owns Visible, so a second dialog would mean teaching nineteen call sites a
    ///     chart-change callback for what is one piece of state in here.
    /// </summary>
    [Fact]
    public void TappingASimilarChartSwapsTheDialogAndLeavesACrumbBack()
    {
        var anchor = SetupChart(null);
        var neighbour = ChartSlugsTests.BuildChart(song: "TRICKL4SH 220");
        _mediator.Setup(m => m.Send(It.IsAny<GetSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSimilarityRecord(neighbour.Id, 0.81, 0.8, 0.8, Array.Empty<ChartSharedBadgeRecord>())
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { neighbour });

        var cut = RenderDialog(anchor, ChartDetailsDialog.DetailsTab.Stats);

        cut.WaitForAssertion(() => cut.Find("[data-testid='chart-similar-tile']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(neighbour.Song.Name.ToString(), cut.Find(".chart-details-title").TextContent);
            Assert.Contains(anchor.Song.Name.ToString(), cut.Find(".chart-details-crumb").TextContent);
        });
    }

    /// <summary>
    ///     A chart with no edges is every chart until the nightly rebuild has run at least
    ///     once, so the panel says "not yet" rather than rendering an empty grid.
    /// </summary>
    [Fact]
    public void AChartWithNoSimilarityEdgesSaysSoRatherThanShowingAnEmptyGrid()
    {
        var cut = RenderDialog(SetupChart(null), ChartDetailsDialog.DetailsTab.Stats);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".chart-similar-empty")));
        Assert.Empty(cut.FindAll("[data-testid='chart-similar-tile']"));
    }

    private void SetupStepChart(StepChartVisibility visibility = StepChartVisibility.Full)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartStepChartRecord("82626", 5, true, visibility, 100, 99,
                Array.Empty<StepChartRowRecord>(), Array.Empty<StepChartHoldRecord>(),
                Array.Empty<decimal>(), Array.Empty<StepChartSegmentRecord>(),
                Array.Empty<StepChartRangeRecord>()));
    }

    /// <summary>
    ///     The Steps tab exists exactly where the chart page's section does: when the mix's
    ///     verdict banked a timeline (step-chart-failure-map.md D14). The default mediator
    ///     answers null, so every other test in this file doubles as the absent case.
    /// </summary>
    [Fact]
    public void StepsTabAppearsOnlyWhenATimelineIsBanked()
    {
        SetupStepChart();
        var cut = RenderDialog(SetupChart(null));

        var tab = cut.Find("[data-testid=cdt-tab-Steps]");
        Assert.Equal("Steps", tab.TextContent.Trim());
    }

    [Fact]
    public void NoTimelineMeansNoStepsTab()
    {
        var cut = RenderDialog(SetupChart(null));

        Assert.Empty(cut.FindAll("[data-testid=cdt-tab-Steps]"));
    }

    [Fact]
    public async Task ActivatingStepsMountsTheCompactShell()
    {
        SetupStepChart(StepChartVisibility.StepsOnly);
        var cut = RenderDialog(SetupChart(null));

        await cut.Find("[data-testid=cdt-tab-Steps]").ClickAsync(new MouseEventArgs());

        var shell = cut.Find("[data-stepchart]");
        Assert.Equal("1", shell.GetAttribute("data-compact"));
        Assert.Equal("StepsOnly", shell.GetAttribute("data-visibility"));
        Assert.Empty(cut.FindAll(".stepchart-minicol"));
    }
}
