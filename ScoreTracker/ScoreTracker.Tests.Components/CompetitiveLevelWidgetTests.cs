using System;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Competitive Level graph's validity floor: a competitive level at or below 5 is the
///     calculator's no-data territory, and a pool with nothing above it must draw nothing at
///     all — the field report was a never-played doubles flatline pinning the y-axis to ~0 so
///     a real singles climb rendered as "no change".
/// </summary>
public sealed class CompetitiveLevelWidgetTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();

    public CompetitiveLevelWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerRatingRecord>());
        Services.AddSingleton(_mediator.Object);
    }

    private void SetUpHistory(params (double Singles, double Doubles)[] points)
    {
        SetUpHistoryWithPasses(points.Select(p => (p, 100)).ToArray());
    }

    private void SetUpHistoryWithPasses(params ((double Singles, double Doubles) Levels, int Passes)[] points)
    {
        // Recent dates so every point sits inside any range window.
        var rows = points.Select((p, i) => new PlayerRatingRecord(_me,
            DateTimeOffset.Now.AddDays(-points.Length + i), (p.Levels.Singles + p.Levels.Doubles) / 2,
            p.Levels.Singles, p.Levels.Doubles, 0, p.Passes)).ToArray();
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    private IRenderedComponent<CompetitiveLevelWidget> Render()
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "competitive-level", null, 0, "2x2", "{}", 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<CompetitiveLevelWidget>(0);
            builder.AddAttribute(1, nameof(CompetitiveLevelWidget.Widget), widget);
            builder.AddAttribute(2, nameof(CompetitiveLevelWidget.EffectiveMix), MixEnum.Phoenix);
            builder.CloseComponent();
        }).FindComponent<CompetitiveLevelWidget>();
    }

    [Fact]
    public void ANeverPlayedPoolDrawsNoSeriesInsteadOfAFloorFlatline()
    {
        SetUpHistory((17.4, 1.0), (17.6, 1.0), (17.9, 1.0));

        var cut = Render();

        var series = cut.FindComponents<ApexCharts.ApexPointSeries<PlayerRatingRecord>>();
        var one = Assert.Single(series);
        Assert.Contains("Singles", one.Instance.Name);
    }

    [Fact]
    public void FloorPointsDropOutOfASeriesThatLaterComesAlive()
    {
        // The doubles pool starts existing on the third row — the first two are the no-data
        // floor and never plot, so the series begins where the data does.
        SetUpHistory((17.4, 1.0), (17.6, 1.0), (17.8, 12.3), (17.9, 12.6));

        var cut = Render();

        var series = cut.FindComponents<ApexCharts.ApexPointSeries<PlayerRatingRecord>>();
        Assert.Equal(2, series.Count);
        var doubles = Assert.Single(series, s => s.Instance.Name!.Contains("Doubles"));
        Assert.Equal(2, doubles.Instance.Items!.Count());
    }

    [Fact]
    public void ReadingsBeforeTwentyPassesAreEstimatorChaosAndDoNotPlot()
    {
        // The first-session estimate swings wildly on a tiny pool (owner field test: a dip
        // from 19.5 to 17.2 and back inside days). The pass-count gate is per-date — the
        // early rows drop, the seasoned ones plot.
        SetUpHistoryWithPasses(
            ((19.5, 18.0), 4), ((17.2, 15.5), 11),
            ((17.8, 16.0), 25), ((17.9, 16.2), 40), ((18.1, 16.5), 60));

        var cut = Render();

        var series = cut.FindComponents<ApexCharts.ApexPointSeries<PlayerRatingRecord>>();
        Assert.Equal(2, series.Count);
        Assert.All(series, s => Assert.Equal(3, s.Instance.Items!.Count()));
    }

    [Fact]
    public void HistoryEntirelyAtTheFloorShowsTheEmptyState()
    {
        SetUpHistory((1.0, 1.0), (1.0, 1.0), (1.0, 1.0));

        var cut = Render();

        Assert.Contains("Your level history starts tracking with your next import.", cut.Markup);
    }
}
