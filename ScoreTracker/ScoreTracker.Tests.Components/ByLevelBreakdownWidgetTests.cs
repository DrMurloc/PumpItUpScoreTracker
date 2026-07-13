using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ByLevelBreakdownWidgetTests : ComponentTestBase
{
    private static Chart Chart(Guid id, ChartType type, int level) =>
        new(id, MixEnum.Phoenix2, new Song("Song", SongType.Arcade, new Uri("https://x/y.png"),
            TimeSpan.FromMinutes(2), "Artist", null), type, level, MixEnum.Phoenix2, null, null, new HashSet<Skill>());

    private void SetupServices(IEnumerable<Chart> charts, IEnumerable<RecordedPhoenixScore> records)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(charts);
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
        Services.AddSingleton(new ByLevelDataSource(mediator.Object, new ChartCatalogCache(mediator.Object)));

        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>())).ReturnsAsync((string?)null);
        settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        Services.AddSingleton(settings.Object);
    }

    private static HomePageWidgetRecord Widget(string configJson = "") =>
        new(Guid.NewGuid(), "by-level-breakdown", null, 0, "2x2", configJson, 1);

    [Fact]
    public void ShowsEmptyStateWhenNotLoggedIn()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        SetupServices(Array.Empty<Chart>(), Array.Empty<RecordedPhoenixScore>());

        var cut = RenderComponent<ByLevelBreakdownWidget>(p => p
            .Add(x => x.Widget, Widget())
            .Add(x => x.EffectiveMix, MixEnum.Phoenix2));

        Assert.Contains("record or import", cut.Markup);
    }

    [Fact]
    public void RendersAChartWhenClearedDataExists()
    {
        var user = new User(Guid.NewGuid(), "Test", true, null, new Uri("https://x/y.png"), null);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(user);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        SetupServices(
            new[] { Chart(s1, ChartType.Single, 20), Chart(s2, ChartType.Single, 20) },
            new[]
            {
                new RecordedPhoenixScore(s1, 950_000, PhoenixPlate.MarvelousGame, false, default),
                new RecordedPhoenixScore(s2, 1_000_000, PhoenixPlate.PerfectGame, false, default)
            });

        var cut = RenderComponent<ByLevelBreakdownWidget>(p => p
            .Add(x => x.Widget, Widget())
            .Add(x => x.EffectiveMix, MixEnum.Phoenix2));

        Assert.Contains("dash-chart-fill", cut.Markup);
        Assert.DoesNotContain("record or import", cut.Markup);
    }

    [Fact]
    public void RendersADistributionBandWithoutError()
    {
        var user = new User(Guid.NewGuid(), "Test", true, null, new Uri("https://x/y.png"), null);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(user);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        SetupServices(
            new[] { Chart(a, ChartType.Single, 20), Chart(b, ChartType.Single, 20), Chart(c, ChartType.Single, 20) },
            new[]
            {
                new RecordedPhoenixScore(a, 900_000, PhoenixPlate.MarvelousGame, false, default),
                new RecordedPhoenixScore(b, 950_000, PhoenixPlate.MarvelousGame, false, default),
                new RecordedPhoenixScore(c, 1_000_000, PhoenixPlate.PerfectGame, false, default)
            });
        var config = WidgetConfigJson.Write(new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Distribution,
            Series = new() { DistributionSeries.Median },
            Band = BreakdownBand.MinMax,
            SeparateSinglesDoubles = false,
            MinLevel = 20,
            MaxLevel = 20
        });

        var cut = RenderComponent<ByLevelBreakdownWidget>(p => p
            .Add(x => x.Widget, Widget(config))
            .Add(x => x.EffectiveMix, MixEnum.Phoenix2));

        Assert.Contains("dash-chart-fill", cut.Markup);
    }

    [Fact]
    public void LoggedInWithNoClearedScoresFallsBackToEmptyState()
    {
        var user = new User(Guid.NewGuid(), "Test", true, null, new Uri("https://x/y.png"), null);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(user);
        // Catalog has charts, but the player has cleared none → Distribution has nothing to plot.
        SetupServices(new[] { Chart(Guid.NewGuid(), ChartType.Single, 20) }, Array.Empty<RecordedPhoenixScore>());

        var cut = RenderComponent<ByLevelBreakdownWidget>(p => p
            .Add(x => x.Widget, Widget())
            .Add(x => x.EffectiveMix, MixEnum.Phoenix2));

        Assert.Contains("record or import", cut.Markup);
    }
}
