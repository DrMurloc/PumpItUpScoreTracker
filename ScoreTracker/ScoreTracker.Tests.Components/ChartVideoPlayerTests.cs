using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor.Services;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The hero jacket's video gesture. It is deliberately the same one the shelf's cards
///     use (<see cref="SimilarChartCardTests" />) — two ways to start a video on one page is
///     one too many — so these pin the parts that drifted apart once already: the jacket
///     itself is the button, it autoplays, and it closes back to the art.
/// </summary>
public sealed class ChartVideoPlayerTests : TestContext
{
    private readonly Mock<IMediator> _mediator = new();

    public ChartVideoPlayerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        // The bubble nested in the jacket injects this and reads through it.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddScoped<ChartScoringLevels>();
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
        // Last: it reads the renderer, locking the service collection. The jacket and its
        // bubble render on their interactive path (RendererInfo gates the bubble's tooltip).
        this.RenderInteractive();
    }

    private Chart SetupChart(string? videoUrl)
    {
        var chart = ChartSlugsTests.BuildChart(song: "Anchor");
        // The island self-loads its chart by id (islands take only serializable params).
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Chart>)new[] { chart });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChartVideoInformation>)(videoUrl == null
                ? Array.Empty<ChartVideoInformation>()
                : new[] { new ChartVideoInformation(chart.Id, new Uri(videoUrl), Name.From("Some Channel")) }));
        return chart;
    }

    private IRenderedComponent<ChartVideoPlayer> Render(Chart chart) =>
        RenderComponent<ChartVideoPlayer>(p => p.Add(c => c.ChartId, chart.Id));

    [Fact]
    public void TheJacketItselfIsThePlayButton()
    {
        // Not a captioned button parked under the art — the same gesture as the shelf card,
        // where the whole jacket is the target and a circle says so.
        var cut = Render(SetupChart("https://www.youtube.com/embed/abc"));

        Assert.NotNull(cut.Find("button.chart-jacket-playable .chart-jacket-art"));
        Assert.NotNull(cut.Find("button.chart-jacket-playable .chart-jacket-play"));
    }

    [Fact]
    public void WatchingAutoplaysRatherThanLandingOnAPausedEmbed()
    {
        // A click that loads a stopped video asks for a second click. The card autoplays;
        // so does this.
        var cut = Render(SetupChart("https://www.youtube.com/embed/abc"));

        cut.Find("button.chart-jacket-playable").Click();

        var src = cut.Find("iframe.chart-jacket-video").GetAttribute("src");
        Assert.Equal("https://www.youtube.com/embed/abc?autoplay=1", src);
        Assert.Contains("autoplay", cut.Find("iframe.chart-jacket-video").GetAttribute("allow")!);
    }

    [Fact]
    public void TheVideoClosesBackToTheArt()
    {
        // The old hero had no way back: once playing, the jacket was gone for good.
        var cut = Render(SetupChart("https://www.youtube.com/embed/abc"));
        cut.Find("button.chart-jacket-playable").Click();

        cut.Find("button.chart-jacket-close").Click();

        Assert.Empty(cut.FindAll("iframe.chart-jacket-video"));
        Assert.NotNull(cut.Find("button.chart-jacket-playable"));
    }

    [Fact]
    public void NoVideoMeansAnInertJacketRatherThanADeadButton()
    {
        var cut = Render(SetupChart(null));

        Assert.Empty(cut.FindAll("button.chart-jacket-playable"));
        Assert.Empty(cut.FindAll(".chart-jacket-play"));
        Assert.NotNull(cut.Find(".chart-jacket-art"));
    }

    [Fact]
    public void TheBubbleLabelsTheArtAndVanishesWithIt()
    {
        // It names the jacket's difficulty; while the video is playing there is no jacket
        // for it to name.
        var cut = Render(SetupChart("https://www.youtube.com/embed/abc"));
        Assert.NotNull(cut.Find(".chart-jacket-bubble"));

        cut.Find("button.chart-jacket-playable").Click();

        Assert.Empty(cut.FindAll(".chart-jacket-bubble"));
    }
}
