using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Pages.Competition.MoM;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Planner (docs/design/march-of-murlocs.md §11.5): it opens on your record book with an
///     empty set, suggesting is an offer rather than the starting position, every number recomputes
///     from what is ticked, and a Phoenix planner says whose grade table it is pricing on.
/// </summary>
public sealed class MoMPlannerPageTests : ComponentTestBase
{
    private static readonly Guid Board = Guid.NewGuid();
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(105);
    private readonly Mock<IUiSettingsAccessor> _settings = new();
    private MixEnum _mix = MixEnum.Phoenix;

    public MoMPlannerPageTests()
    {
        _settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mix);
        _settings.Setup(s => s.GetSetting(It.IsAny<string>())).ReturnsAsync((string?)null);
        Services.AddSingleton(_settings.Object);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("DRMURLOC"), true, null,
            new Uri("https://example.invalid/a.png"), null));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private static Chart Chart(string name, int level, int seconds) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/s.png"),
                TimeSpan.FromSeconds(seconds), Name.From("artist"), null),
            ChartType.Double, DifficultyLevel.From(level), MixEnum.Phoenix, null, null);

    private static MoMPlanChartView Row(Chart chart, int points, bool inSet, RestChartFacts? rest = null,
        bool closing = false) =>
        new(chart, 980000, PhoenixPlate.MarvelousGame, false, points, points / chart.Song.Duration.TotalSeconds,
            (int)chart.Level + .5, inSet, closing, rest);

    private void Plan(IReadOnlyList<MoMPlanChartView>? charts = null, MixEnum mix = MixEnum.Phoenix,
        int? banked = null, MoMEnergy energy = MoMEnergy.Good)
    {
        _mix = mix;
        var rows = charts ?? new[]
        {
            Row(Chart("Slam", 24, 128), 1450, true),
            Row(Chart("Gargoyle", 20, 115), 650, true),
            Row(Chart("Left Behind", 22, 110), 930, false)
        };
        var inSet = rows.Where(r => r.InSet).ToArray();
        var season = new MoMSeasonSummary(Guid.NewGuid(), "Summer 2026", DateTimeOffset.Now.AddDays(-10),
            DateTimeOffset.Now.AddDays(50), true);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSeasonPage(season, new[]
            {
                new MoMBoardView(Board, ChartType.Double, mix, Window, Array.Empty<MoMBoardRow>(), null),
                new MoMBoardView(Guid.NewGuid(), ChartType.Single, mix, Window, Array.Empty<MoMBoardRow>(), null)
            }, null, null));
        Mediator.Setup(m => m.Send(It.IsAny<BuildMoMPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMPlanView(Board, "Summer 2026", mix, ChartType.Double, Window,
                TimeSpan.FromSeconds(35), energy, MoMPush.Steady, 23, 23.67,
                inSet.Sum(r => r.Points), inSet.Length,
                inSet.Length == 0 ? 0 : inSet.Average(r => r.BalancedLevel), 980000,
                TimeSpan.FromMinutes(20), banked, rows));
    }

    private IRenderedComponent<Planner> Render() => RenderComponent<Planner>();

    [Fact]
    public void ItOpensOnYourRecordBookWithAnEmptySet()
    {
        Plan();

        var cut = Render();

        Assert.Equal(3, cut.FindAll("[data-testid=mom-plan-row]").Count);
        // Suggesting is an offer, not the starting position: nothing is planned until asked.
        Assert.Equal("0", cut.Find(".pl-proj-big").TextContent);
        Assert.True(cut.Find("[data-testid=mom-plan-csv]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=mom-plan-save]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task SuggestingASetFillsTheNumbersFromWhatTheSolverPicked()
    {
        Plan();
        var cut = Render();

        await cut.Find("[data-testid=mom-plan-suggest]").ClickAsync(new());

        Assert.Equal("2,100", cut.Find(".pl-proj-big").TextContent);
        Assert.False(cut.Find("[data-testid=mom-plan-csv]").HasAttribute("disabled"));
        // A suggested set is not hand-built, so the slider still retunes it.
        Assert.Empty(cut.FindAll("[data-testid=mom-plan-handpicked]"));
    }

    [Fact]
    public async Task AHandTickTakesTheSetOffAutopilotAndEveryNumberFollowsIt()
    {
        Plan();
        var cut = Render();

        // The jacket opens the chart, so the corner tick is what picks it.
        await cut.FindAll("[data-testid=mom-plan-tick]")[2].ClickAsync(new());

        Assert.Equal("930", cut.Find(".pl-proj-big").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid=mom-plan-handpicked]"));
    }

    [Fact]
    public void APhoenixPlannerSaysWhoseGradeTableItIsPricingOn()
    {
        Plan();

        var cut = Render();

        Assert.Contains("mostly below AAA", cut.Find("[data-testid=mom-plan-mixnote]").TextContent);
    }

    [Fact]
    public void APhoenixTwoPlannerHasNothingToDisclaim()
    {
        Plan(mix: MixEnum.Phoenix2);

        var cut = Render();

        Assert.Empty(cut.FindAll("[data-testid=mom-plan-mixnote]"));
    }

    [Fact]
    public async Task TheConversionAppearsOnlyOnceThereIsASetToCompare()
    {
        Plan(banked: 1050);
        var cut = Render();
        Assert.Empty(cut.FindAll("[data-testid=mom-plan-conversion]"));

        await cut.Find("[data-testid=mom-plan-suggest]").ClickAsync(new());

        Assert.Contains("50%", cut.Find("[data-testid=mom-plan-conversion]").TextContent);
    }

    [Fact]
    public void TheRestShelfListsOnlyChartsCatalogCallsRestCharts()
    {
        var slam = Chart("Slam", 24, 128);
        var busy = Chart("Busy", 24, 120);
        Plan(new[]
        {
            Row(slam, 1450, false,
                new RestChartFacts(slam.Id, true, 4.7, 6, true, .53, 79, true, true, 0, true, 2.9, 7, true)),
            Row(busy, 1400, false,
                new RestChartFacts(busy.Id, false, 9.1, 88, false, .2, 20, false, true, 0, true, 8.4, 91, false))
        });

        var cut = Render();

        var shelf = cut.Find("[data-testid=mom-plan-rest-shelf]");
        var rows = shelf.QuerySelectorAll("[data-testid=mom-plan-shelf-row]");
        Assert.Single(rows);
        Assert.Contains("Slam", rows[0].TextContent);
    }

    [Fact]
    public void AFinisherIsThreeMinutesOrMore()
    {
        Plan(new[]
        {
            Row(Chart("Long one", 24, 200), 1450, false),
            Row(Chart("Short one", 24, 100), 1450, false)
        });

        var cut = Render();

        var rows = cut.Find("[data-testid=mom-plan-finishers]")
            .QuerySelectorAll("[data-testid=mom-plan-shelf-row]");
        Assert.Single(rows);
        Assert.Contains("Long one", rows[0].TextContent);
    }

    [Fact]
    public async Task ClickingAChartOpensItRatherThanPickingIt()
    {
        Plan();
        var cut = Render();

        await cut.FindAll("[data-testid=mom-plan-row]")[0].ClickAsync(new());

        // Nothing was picked, and the chart dialog is what came up.
        Assert.Equal("0", cut.Find(".pl-proj-big").TextContent);
        Assert.NotEmpty(cut.FindComponents<ChartDetailsDialog>()
            .Where(d => d.Instance.Visible));
    }

    [Fact]
    public void ProjectedScoresAreOffUntilAskedFor()
    {
        Plan();

        var cut = Render();

        Assert.Contains("Include projected scores", cut.Find("[data-testid=mom-plan-controls]").TextContent);
        // Off is the default, and the query is what actually keeps them out of the book.
        Mediator.Verify(m => m.Send(It.Is<BuildMoMPlanQuery>(q => !q.IncludeProjected),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Mediator.Verify(m => m.Send(It.Is<BuildMoMPlanQuery>(q => q.IncludeProjected),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AProjectedChartSaysSoOnItsSticker()
    {
        var chart = Chart("Never played", 22, 120);
        Plan(new[] { Row(chart, 930, false) with { IsProjected = true } });

        var cut = Render();

        Assert.Contains("Projected", cut.Find("[data-testid=mom-plan-row]").TextContent);
    }

    [Fact]
    public void ADifficultyIsWrittenTheWayTheSiteWritesIt()
    {
        Plan();

        var cut = Render();

        var push = cut.Find("[data-testid=mom-plan-push]").TextContent;
        // D23, not "Doubles 23.67": a folder, not a measurement.
        Assert.Contains("D23", push);
        Assert.DoesNotContain("23.67", push);
        Assert.DoesNotContain("Doubles 23", push);
    }

    [Fact]
    public void AutoSelectSitsWithTheSetRatherThanWithTheControls()
    {
        Plan();

        var cut = Render();

        var button = cut.Find("[data-testid=mom-plan-suggest]");
        Assert.Contains("Auto-select set", button.TextContent);
        Assert.Empty(cut.Find("[data-testid=mom-plan-controls]")
            .QuerySelectorAll("[data-testid=mom-plan-suggest]"));
    }

    [Fact]
    public void ASignedOutVisitorIsAskedToSignInRatherThanShownAnEmptyBook()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var cut = Render();

        Assert.Contains("Sign in", cut.Find("[data-testid=mom-plan-empty]").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=mom-plan-controls]"));
    }
}
