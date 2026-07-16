using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using MediatR;
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
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
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

    public ChartDetailsDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_notifications.Object);
        Services.AddSingleton(_currentUser.Object);
        _currentUser.Setup(u => u.IsLoggedIn).Returns(false);
        // The bubble nested in the title row injects this and reads through it.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddScoped<ChartScoringLevels>();
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>());
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
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
    private IRenderedFragment RenderDialog(Chart chart)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartDetailsDialog>(1);
            builder.AddAttribute(2, nameof(ChartDetailsDialog.Chart), chart);
            builder.AddAttribute(3, nameof(ChartDetailsDialog.Visible), true);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ReportingAVideoNamesTheChartToAnAdmin()
    {
        var chart = SetupChart("https://www.youtube.com/embed/abc");
        var cut = RenderDialog(chart);

        cut.WaitForAssertion(() => cut.Find(".chart-details-video-report button").Click());

        _notifications.Verify(n => n.NotifyAdmin(
            It.Is<string>(m => m.Contains(chart.Song.Name.ToString()) && m.Contains(chart.DifficultyString)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NoVideoMeansNothingToReport()
    {
        var cut = RenderDialog(SetupChart(null));

        // The dialog rendered (its meta grid is there) but no report affordance exists —
        // reporting is the video's action, not the chart's.
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".chart-details-meta")));
        Assert.Empty(cut.FindAll(".chart-details-video-report"));
    }
}
