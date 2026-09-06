using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Competition.MoM;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Submit (docs/design/march-of-murlocs.md §11.4): the budget bar that fills with song time and
///     only blocks on the charts before the closing one, the session list with its ordinals and
///     points, and the published state that freezes the page.
/// </summary>
public sealed class MoMSubmitPageTests : ComponentTestBase
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(105);

    public MoMSubmitPageTests()
    {
        Services.AddSingleton(Mock.Of<IUiSettingsAccessor>());
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        // Submit is a circuit page: its controls only exist once one is attached.
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private static Chart Chart(string name, int level, int seconds) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/s.png"),
                TimeSpan.FromSeconds(seconds), Name.From("artist"), null),
            ChartType.Double, DifficultyLevel.From(level), MixEnum.Phoenix, null, null);

    private static MoMSessionChart Row(Chart chart, int score, int points) =>
        new(chart, score, PhoenixPlate.MarvelousGame, false, points, 0, (int)chart.Level + .5, null);

    private void Draft(IReadOnlyList<MoMSessionChart>? charts = null, bool published = false,
        TimeSpan? songTime = null, TimeSpan? beforeLast = null)
    {
        var rows = charts ?? new[] { Row(Chart("Gargoyle", 20, 115), 986121, 868) };
        var song = songTime ?? TimeSpan.FromTicks(rows.Sum(r => r.Chart.Song.Duration.Ticks));
        var view = new MoMDraftView(SessionId, BoardId, Guid.NewGuid(), "Summer 2026", MixEnum.Phoenix2,
            ChartType.Double, published, Window, song,
            beforeLast ?? (rows.Count == 0 ? TimeSpan.Zero : song - rows[^1].Chart.Song.Duration),
            rows.Sum(r => r.SessionScore), null, rows);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMDraftQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
    }

    private IRenderedComponent<Submit> Render() =>
        RenderComponent<Submit>(p => p.Add(x => x.SessionId, SessionId));

    [Fact]
    public void TheDraftLeadsWithTheBudgetAndSaysWhoCanSeeIt()
    {
        Draft();

        var cut = Render();

        Assert.Contains("Draft · only you can see this", cut.Find("[data-testid=mom-submit-state]").TextContent);
        Assert.Contains("Phoenix 2", cut.Find(".pmb-eyebrow").TextContent);
        Assert.Contains("Summer 2026", cut.Find(".pmb-eyebrow").TextContent);
        var budget = cut.Find("[data-testid=mom-budget]");
        Assert.Contains("0:01:55", budget.TextContent);
        Assert.Contains("of 1:45:00", budget.TextContent);
        Assert.DoesNotContain("blocked", budget.ClassName);
        Assert.Contains("A chart may start until the bar is full", budget.TextContent);
    }

    [Fact]
    public void ASessionWhoseChartsBeforeTheLastFillTheWindowCannotTakeAnother()
    {
        // 1:46 of song before the closing chart: the window governs when a chart may START.
        Draft(songTime: TimeSpan.FromMinutes(110), beforeLast: TimeSpan.FromMinutes(106));

        var cut = Render();

        var budget = cut.Find("[data-testid=mom-budget]");
        Assert.Contains("blocked", budget.ClassName);
        Assert.Contains("Window full", budget.TextContent);
        Assert.Contains("Nothing more can start.", budget.TextContent);
    }

    [Fact]
    public void TheClosingChartOverhangsAndTheWindowIsFullBehindIt()
    {
        // 1:47 of song, of which 1:43 came before the closing chart: that chart started inside the
        // window and ran past it, which is what §1 allows. The session is finished, not spoiled.
        Draft(songTime: TimeSpan.FromMinutes(107), beforeLast: TimeSpan.FromMinutes(103));

        var cut = Render();

        var budget = cut.Find("[data-testid=mom-budget]");
        Assert.Contains("closing chart", cut.Find("[data-testid=mom-session-row]").TextContent);
        // Nothing else can start, which is what the bar has to say — this is the state every
        // completed session ends in, and it read "0:00 open" until the predicate was fixed.
        Assert.Contains("Window full", budget.TextContent);
    }

    [Fact]
    public void EveryChartIsARowWithItsPositionAndItsPoints()
    {
        Draft(new[]
        {
            Row(Chart("Gargoyle", 20, 115), 986121, 868),
            Row(Chart("Ugly Dee", 17, 96), 970915, 368),
            Row(Chart("4NT", 20, 105), 992796, 838)
        });

        var cut = Render();

        var rows = cut.FindAll("[data-testid=mom-session-row]");
        Assert.Equal(3, rows.Count);
        Assert.Contains("1", rows[0].QuerySelector(".mom-ordn")!.TextContent);
        Assert.Contains("Ugly Dee", rows[1].TextContent);
        Assert.Contains("368 pts", rows[1].TextContent);
        Assert.Contains("2,074", cut.Find(".mom-sess-total").TextContent);
    }

    [Fact]
    public void AnEmptyDraftSaysSoAndCannotBePublished()
    {
        Draft(Array.Empty<MoMSessionChart>());

        var cut = Render();

        Assert.NotEmpty(cut.FindAll("[data-testid=mom-session-empty]"));
        Assert.True(cut.Find("[data-testid=mom-publish]").HasAttribute("disabled"));
    }

    [Fact]
    public void ThePublishedStateReplacesTheFormAndOffersTheImageFirst()
    {
        Draft(published: true);

        var cut = Render();

        Assert.Contains("on the board", cut.Find("[data-testid=mom-submit-state]").TextContent);
        var published = cut.Find("[data-testid=mom-published]");
        Assert.Contains("Frozen: to change it, delete and record again.", published.TextContent);
        Assert.Contains("Download image", published.QuerySelectorAll("a")[0].TextContent);
        // Nothing to enter, nothing to remove, nothing to publish twice.
        Assert.Empty(cut.FindAll("[data-testid=mom-entry]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-budget]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-session-remove]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-publish]"));
    }

    [Fact]
    public void SomeoneElsesSessionIsNotDrawnAtAll()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMDraftQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMDraftView?)null);

        var cut = Render();

        Assert.NotEmpty(cut.FindAll("[data-testid=mom-submit-missing]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-session]"));
    }

    [Fact]
    public void ASignedOutVisitorIsToldNothingAboutTheSession()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        Draft();

        var cut = Render();

        Assert.NotEmpty(cut.FindAll("[data-testid=mom-submit-missing]"));
        Mediator.Verify(m => m.Send(It.IsAny<GetMoMDraftQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
