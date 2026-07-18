using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.Web.Components.Challenges;
using ScoreTracker.Web.Enums;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The static challenges-hub section components (weekly-charts-overhaul.md §4). These are
///     pure display — parameters in, markup out — so bUnit pins the anatomy that the acceptance
///     list cares about: empty states, the density variants, the unplayed dash, the suggested
///     border, the Limbo treatment, and the legend's per-mix plate ladder.
/// </summary>
public sealed class ChallengeComponentsTests : ComponentTestBase
{
    public ChallengeComponentsTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        // These sections nest DifficultyBubble/ScoreBreakdown/UserLabel, which gate their
        // MudTooltip on RendererInfo; declare the render world so bUnit can supply it.
        // (SetRendererInfo builds the service provider, so every registration lands first.)
        this.RenderInteractive();
    }

    private static Chart MakeChart(string name = "District 1", ChartType type = ChartType.Single, int level = 20) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(160, 160)),
            type, level, MixEnum.Phoenix, null, 900, new HashSet<Skill>());

    private static User MakeUser(string name = "HYNIX") =>
        new(Guid.NewGuid(), Name.From(name), true, null, new Uri("https://piu.test/a.png"), Name.From("KR"));

    private static WeeklyTournamentEntry Entry(Guid chartId, Guid userId, int score) =>
        new(userId, chartId, score, PhoenixPlate.SuperbGame, false, null, 18.0);

    // ---- DailyStepStrip -----------------------------------------------------

    [Fact]
    public void DailyStripShowsTheMidnightEmptyStateWithoutABoard()
    {
        var cut = RenderComponent<DailyStepStrip>(p => p
            .Add(x => x.View, null)
            .Add(x => x.Chart, null));

        Assert.Contains("posts at midnight", cut.Markup);
        Assert.Empty(cut.FindAll(".challenge-daily-strip"));
    }

    [Fact]
    public void DailyStripWearsTheLimboTreatmentOnLimboDays()
    {
        var chart = MakeChart("Butterfly", ChartType.Single, 3);
        var board = new DailyStepBoard(chart.Id, DateTimeOffset.UtcNow, IsLimbo: true, DateTimeOffset.UtcNow.AddHours(6));
        var rows = new[]
        {
            new DailyStepBoardRow(1, MakeUser(), new DailyStepEntry(Guid.NewGuid(), chart.Id, 610_000,
                PhoenixPlate.RoughGame, false, 18, ChallengeEntrySource.Official))
        };
        var view = new DailyStepBoardView(board, rows, null);

        var cut = RenderComponent<DailyStepStrip>(p => p
            .Add(x => x.View, view).Add(x => x.Chart, chart).Add(x => x.IsLoggedIn, false));

        Assert.Single(cut.FindAll(".challenge-daily-strip.limbo"));
        Assert.Contains("lowest pass wins", cut.Markup);
    }

    // ---- WeeklyBoardGrid ----------------------------------------------------

    private static WeeklyBoardChartSummary Summary(Chart chart, bool suggested, WeeklyBoardRow? mine)
    {
        var top = new WeeklyBoardRow(1, MakeUser(), Entry(chart.Id, Guid.NewGuid(), 990_000),
            ChallengeEntrySource.Official);
        return new WeeklyBoardChartSummary(chart.Id, DateTimeOffset.UtcNow.AddDays(3), 12,
            new[] { top }, mine, suggested, new[] { top }, 12);
    }

    [Fact]
    public void WeeklyGridRendersTheDensityVariantItIsGiven()
    {
        var chart = MakeChart();
        var view = new[] { Summary(chart, suggested: true, mine: null) };

        var cut = RenderComponent<WeeklyBoardGrid>(p => p
            .Add(x => x.Summaries, view).Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.Density, UiDensity.Table).Add(x => x.IsLoggedIn, false).Add(x => x.TotalCount, 1));

        Assert.Equal("Table", cut.Find(".challenge-grid").GetAttribute("data-density"));
        Assert.Contains("on", cut.Find("[data-den=\"Table\"]").GetAttribute("class"));
    }

    [Fact]
    public void WeeklyCardMarksSuggestedWithTheBorderAttributeAndShowsTheUnplayedDash()
    {
        var chart = MakeChart();
        var view = new[] { Summary(chart, suggested: true, mine: null) };

        var cut = RenderComponent<WeeklyBoardGrid>(p => p
            .Add(x => x.Summaries, view).Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.Density, UiDensity.Comfortable).Add(x => x.IsLoggedIn, true).Add(x => x.TotalCount, 1));

        Assert.Equal("true", cut.Find(".challenge-card").GetAttribute("data-suggested"));
        var unplayed = cut.Find(".challenge-card-line.unplayed");
        Assert.Equal("—", unplayed.TextContent.Trim());
    }

    [Fact]
    public void WeeklyGridEmptyStateNamesTheMondayRotation()
    {
        var cut = RenderComponent<WeeklyBoardGrid>(p => p
            .Add(x => x.Summaries, Array.Empty<WeeklyBoardChartSummary>())
            .Add(x => x.Charts, new Dictionary<Guid, Chart>()).Add(x => x.IsLoggedIn, false));

        Assert.Contains("post Monday at midnight", cut.Markup);
    }

    // ---- MonthlyLeaderboard -------------------------------------------------

    [Fact]
    public void MonthlyEmptyStateShowsWhenNoRows()
    {
        var view = new MonthlyLeaderboardView(Array.Empty<MonthlyLeaderboardRow>(), 1, 4, null, null);

        var cut = RenderComponent<MonthlyLeaderboard>(p => p
            .Add(x => x.View, view).Add(x => x.Charts, new Dictionary<Guid, Chart>()).Add(x => x.Type, null));

        Assert.Contains("Scores land here", cut.Markup);
        // The Co-Op type pill is always present as a navigation link.
        Assert.Contains("Co-Op", cut.Markup);
    }

    // ---- DailyStepHistory ---------------------------------------------------

    [Fact]
    public void HistoryEmptyStateNamesTheAction()
    {
        var cut = RenderComponent<DailyStepHistory>(p => p
            .Add(x => x.Records, Array.Empty<DailyStepHistoryRecord>())
            .Add(x => x.Charts, new Dictionary<Guid, Chart>()));

        Assert.Contains("start your streak", cut.Markup);
    }

    [Fact]
    public void HistoryTagsLimboDays()
    {
        var chart = MakeChart();
        var records = new[]
        {
            new DailyStepHistoryRecord(DateTimeOffset.UtcNow, chart.Id, IsLimbo: true, 2, 44,
                650_000, PhoenixPlate.RoughGame, false)
        };

        var cut = RenderComponent<DailyStepHistory>(p => p
            .Add(x => x.Records, records).Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart }));

        Assert.Single(cut.FindAll(".challenge-limbo-tag"));
    }

    // ---- ChallengeScoringLegend ---------------------------------------------

    [Fact]
    public void LegendShowsThePlateLadderOnlyForPhoenix2()
    {
        var phoenix = RenderComponent<ChallengeScoringLegend>(p => p.Add(x => x.Mix, MixEnum.Phoenix));
        Assert.DoesNotContain("Plate bonuses", phoenix.Markup);

        var phoenix2 = RenderComponent<ChallengeScoringLegend>(p => p.Add(x => x.Mix, MixEnum.Phoenix2));
        Assert.Contains("Plate bonuses", phoenix2.Markup);
    }
}
