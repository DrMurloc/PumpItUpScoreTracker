using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
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
        _mediator.Setup(m => m.Send(It.IsAny<GetChartBadgeChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartBadgeChipRecord>>());
        // The dialog hosts the shared leaderboard now, which resolves the user reader to put
        // names and avatars on its rows.
        Services.AddSingleton(Mock.Of<IUserReader>());
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
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
    ///     badge chips are the cheapest thing to assert on, because the meta grid renders from
    ///     the Chart itself and would be present either way.
    /// </summary>
    [Fact]
    public void AnUnselectedTabFetchesNothing()
    {
        RenderDialog(SetupChart(null));

        _mediator.Verify(m => m.Send(It.IsAny<GetChartBadgeChipsQuery>(), It.IsAny<CancellationToken>()),
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
            _mediator.Verify(m => m.Send(It.IsAny<GetChartBadgeChipsQuery>(), It.IsAny<CancellationToken>()),
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
}
