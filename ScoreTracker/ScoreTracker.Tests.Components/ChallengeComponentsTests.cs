using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
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

    // ---- DailyStepRailCard --------------------------------------------------

    private static DailyStepBoardRow DailyRow(int place, Guid chartId, int score, User? player = null) =>
        new(place, player ?? MakeUser(), new DailyStepEntry((player ?? MakeUser()).Id, chartId, score,
            PhoenixPlate.SuperbGame, false, 18, ChallengeEntrySource.Official));

    [Fact]
    public void DailyRailCardShowsTopFiveAndPinsYourStandingPastThem()
    {
        var chart = MakeChart();
        var me = MakeUser("ME");
        var board = new DailyStepBoard(chart.Id, DateTimeOffset.UtcNow, false, DateTimeOffset.UtcNow.AddHours(6));
        var rows = Enumerable.Range(1, 7).Select(p => DailyRow(p, chart.Id, 999_000 - p * 1000)).ToArray();
        var mine = new DailyStepBoardRow(7, me, rows[6].Entry);
        var view = new DailyStepBoardView(board, rows, mine);

        var cut = RenderComponent<DailyStepRailCard>(p => p
            .Add(x => x.View, view).Add(x => x.Chart, chart)
            .Add(x => x.IsLoggedIn, true).Add(x => x.UserId, me.Id));

        // Five ranked rows, then the pinned standing under its divider.
        Assert.Equal(6, cut.FindAll(".challenge-lb-row").Count);
        Assert.Contains("Your standing", cut.Markup);
        Assert.Single(cut.FindAll(".challenge-lb-row.mine"));
    }

    [Fact]
    public void DailyRailCardWearsTheWidgetIconPair()
    {
        var chart = MakeChart();
        var board = new DailyStepBoard(chart.Id, DateTimeOffset.UtcNow, false, DateTimeOffset.UtcNow.AddHours(6));
        var view = new DailyStepBoardView(board, new[] { DailyRow(1, chart.Id, 990_000) }, null);

        var cut = RenderComponent<DailyStepRailCard>(p => p
            .Add(x => x.View, view).Add(x => x.Chart, chart).Add(x => x.IsLoggedIn, true));

        // Record ⊕ and the trophy (M15) as inert data-challenge controls, plus rows with avatars.
        Assert.Single(cut.FindAll("button.challenge-iconbtn.rec[data-challenge-record]"));
        Assert.Single(cut.FindAll("button.challenge-iconbtn.tro[data-challenge-board]"));
        Assert.Single(cut.FindAll(".challenge-lb-row img.challenge-avatar"));
    }

    // ---- MonthlyRailCard ----------------------------------------------------

    private static MonthlyLeaderboardRow MonthlyRow(int place, User player, double total,
        double competitiveLevel = 21.4) =>
        new(place, player, total, Array.Empty<MonthlyEntry>(), Array.Empty<MonthlyEntry>(), competitiveLevel);

    [Fact]
    public void MonthlyRailCardRendersAllFourBoardsWithOnlyTheActiveVisible()
    {
        var view = new MonthlyLeaderboardView(new[] { MonthlyRow(1, MakeUser(), 3137) }, 1, 4, null, null);
        var boards = new[]
        {
            new MonthlyRailBoard(null, view),
            new MonthlyRailBoard(ChartType.Single, view),
            new MonthlyRailBoard(ChartType.Double, view),
            new MonthlyRailBoard(ChartType.CoOp, view)
        };

        var cut = RenderComponent<MonthlyRailCard>(p => p
            .Add(x => x.Boards, boards).Add(x => x.ActiveType, ChartType.Single));

        Assert.Equal(4, cut.FindAll(".challenge-mboard").Count);
        Assert.Single(cut.FindAll(".challenge-mboard:not([hidden])"));
        Assert.Equal(4, cut.FindAll(".challenge-seg-btn").Count);
        var active = Assert.Single(cut.FindAll(".challenge-seg-btn.on"));
        Assert.Equal("Single", active.GetAttribute("data-mtype"));
    }

    [Fact]
    public void MonthlyRailRowsCarryAvatarCompetitiveLevelAndTotal()
    {
        var me = MakeUser("ME");
        var view = new MonthlyLeaderboardView(new[]
        {
            MonthlyRow(1, MakeUser(), 3137, 21.86),
            MonthlyRow(2, me, 2489, 20.61)
        }, 1, 4, null, null);

        var cut = RenderComponent<MonthlyRailCard>(p => p
            .Add(x => x.Boards, new[] { new MonthlyRailBoard(null, view) })
            .Add(x => x.UserId, me.Id));

        Assert.Equal(2, cut.FindAll(".challenge-lb-row img.challenge-avatar").Count);
        Assert.Contains("21.86", cut.Markup);
        Assert.Contains("3,137", cut.Markup);
        Assert.Single(cut.FindAll(".challenge-lb-row.mine"));
    }

    // ---- MonthlyBoardDialog -------------------------------------------------

    [Fact]
    public void MonthlyDialogStickersCarryNoSongNamesOnlyTooltips()
    {
        // The counted expansion is the official-leaderboards compact pattern (M21): jacket +
        // bubble + grade/score + points; the song's name lives in the tooltip alone.
        var chart = MakeChart("Secret Song");
        var player = new User(Guid.NewGuid(), Name.From("Archi"), true, null,
            new Uri("https://piu.test/a.png"), null);
        var entry = new MonthlyEntry(chart.Id, 992_410, PhoenixPlate.SuperbGame, false, 824);
        var view = new MonthlyLeaderboardView(new[]
        {
            new MonthlyLeaderboardRow(1, player, 3137, new[] { entry }, new[] { entry }, 21.86)
        }, 1, 4, null, null);

        var cut = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MonthlyBoardDialog>(1);
            builder.AddComponentParameter(2, nameof(MonthlyBoardDialog.Visible), true);
            builder.AddComponentParameter(3, nameof(MonthlyBoardDialog.View), view);
            builder.AddComponentParameter(4, nameof(MonthlyBoardDialog.Charts),
                (IReadOnlyDictionary<Guid, Chart>)new Dictionary<Guid, Chart> { [chart.Id] = chart });
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            var sticker = cut.Find(".challenge-stik");
            Assert.Contains("Secret Song", sticker.GetAttribute("title"));
            Assert.DoesNotContain("Secret Song", sticker.QuerySelector(".challenge-stik-line")!.TextContent);
            Assert.Contains("824", sticker.TextContent);
            Assert.Contains("21.86", cut.Markup);
            Assert.Equal(4, cut.FindAll(".challenge-seg-btn").Count);
        });
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
            .Add(x => x.Density, UiDensity.Table).Add(x => x.IsLoggedIn, false));

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
            .Add(x => x.Density, UiDensity.Comfortable).Add(x => x.IsLoggedIn, true));

        Assert.Equal("true", cut.Find(".challenge-card").GetAttribute("data-suggested"));
        var unplayed = cut.Find(".challenge-card-line.unplayed");
        Assert.Equal("—", unplayed.TextContent.Trim());
    }

    [Fact]
    public void WeeklyCardShipsBothWorldsOnItsRows()
    {
        // Overall top-3: sandbagger (out of band, no in-range place), then two in-band rows
        // whose renumbered places trail by one. Every row carries both worlds (M20) so the
        // relevant-players switch is pure CSS.
        var chart = MakeChart();
        var sandbagger = new WeeklyBoardRow(1, MakeUser("SBAG"), Entry(chart.Id, Guid.NewGuid(), 995_000),
            ChallengeEntrySource.Official, WasWithinRange: false, InRangePlace: null);
        var second = new WeeklyBoardRow(2, MakeUser("REAL"), Entry(chart.Id, Guid.NewGuid(), 970_000),
            ChallengeEntrySource.Official, WasWithinRange: true, InRangePlace: 1);
        var third = new WeeklyBoardRow(3, MakeUser("ALSO"), Entry(chart.Id, Guid.NewGuid(), 960_000),
            ChallengeEntrySource.Official, WasWithinRange: true, InRangePlace: 2);
        var summary = new WeeklyBoardChartSummary(chart.Id, DateTimeOffset.UtcNow.AddDays(3), 3,
            new[] { sandbagger, second, third }, null, false, new[] { second, third }, 2);

        var cut = RenderComponent<WeeklyBoardGrid>(p => p
            .Add(x => x.Summaries, new[] { summary })
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart })
            .Add(x => x.IsLoggedIn, false));

        var lines = cut.FindAll(".challenge-card-line");
        Assert.Equal(3, lines.Count);
        var sand = lines.Single(l => l.GetAttribute("data-inrange") == "false");
        Assert.DoesNotContain("w-r", sand.GetAttribute("class"));
        Assert.Contains("—", sand.QuerySelector(".challenge-lb-place.cr")!.TextContent);
        var real = lines.First(l => l.TextContent.Contains("REAL"));
        Assert.Contains("w-o", real.GetAttribute("class"));
        Assert.Contains("w-r", real.GetAttribute("class"));
        Assert.Equal("2", real.QuerySelector(".challenge-lb-place.co")!.TextContent.Trim());
        Assert.Equal("1", real.QuerySelector(".challenge-lb-place.cr")!.TextContent.Trim());
        // Rows carry avatars now (M16 vocabulary on the cards too).
        Assert.Equal(3, cut.FindAll(".challenge-card-line img.challenge-avatar").Count);
        // The footer wears the widget icon pair (M15) — board trophy for the anonymous view.
        Assert.Single(cut.FindAll(".challenge-card-foot .challenge-iconbtn.tro[data-challenge-board]"));
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
