using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.MarchOfMurlocs;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Planner (march-of-murlocs.md §11.5): the pool is your record book priced under the
///     live board's frozen configuration (wrong-type and zero-value rows out), the page opens
///     on Everything with an EMPTY set, Suggest fills it — appending the closing move (§2.9)
///     — and a manual tick takes the set off autopilot.
/// </summary>
public sealed class MoMPlannerPageTests : ComponentTestBase
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly MoMSeasonRef Season = new(Guid.NewGuid(), "Summer 2026", 2026, 3);

    private readonly Chart _doubleA;
    private readonly Chart _doubleB;
    private readonly Chart _single;
    private readonly TournamentConfiguration _configuration;
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public MoMPlannerPageTests()
    {
        _doubleA = BuildChart("Slam", 24, ChartType.Double, TimeSpan.FromSeconds(99));
        _doubleB = BuildChart("Gargoyle - FULL SONG -", 25, ChartType.Double, TimeSpan.FromSeconds(378));
        _single = BuildChart("Pirate", 21, ChartType.Single, TimeSpan.FromSeconds(130));
        _configuration = NeutralConfiguration();

        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>())).ReturnsAsync((string?)null);
        _uiSettings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d =>
            d.Now == new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddScoped<ChartCatalogCache>();

        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(Me, Name.From("Me"), true, null,
            new Uri("https://example.invalid/a.png"), null));

        var season = new MoMSeasonView(Season.Id, Season.Name, 2026, 3,
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30), true,
            new[] { new MoMBoardSummary(BoardId, MixEnum.Phoenix, ChartType.Double, 0) }, null,
            null);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(season);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MoMSeasonListing>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMBoardView(BoardId, Season, MixEnum.Phoenix, ChartType.Double,
                Array.Empty<MoMBoardRow>()));
        Mediator.Setup(m => m.Send(It.IsAny<GetTournamentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_configuration);
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _doubleA, _doubleB, _single });
        Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Record(_doubleA, 976489),
                Record(_doubleB, 844710),
                // The Singles record and the zeroed play never reach a Doubles pool.
                Record(_single, 987859),
                Record(BuildChart("Ghost", 24, ChartType.Double, TimeSpan.FromSeconds(100)), 400000)
            });
        Mediator.Setup(m => m.Send(It.IsAny<AutoBuildSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var session = new TournamentSession(Me, _configuration, MixEnum.Phoenix);
                session.Add(_doubleA, 976489, PhoenixPlate.FairGame, false);
                return session;
            });

        Renderer.SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void OpensOnEverythingWithAnEmptySetAndAFilteredPool()
    {
        var page = RenderComponent<Planner>();

        // Two Doubles with value; the Singles record and the zero-pointer are not offered.
        Assert.Equal(2, page.FindAll(".mom-plan-sticker").Count);
        Assert.Empty(page.FindAll(".mom-plan-sticker.on"));
        Assert.Contains("All 2 charts you hold a scoring record on", page.Markup);
        Assert.Contains("Nothing in your set yet", page.Find(".mom-plan-versus").TextContent);
        Assert.Contains("Suggest a set", page.Markup);
    }

    [Fact]
    public async Task SuggestFillsTheSetAndAppendsTheClosingMove()
    {
        var page = RenderComponent<Planner>();

        await page.FindAll("button").First(b => b.TextContent.Contains("Suggest a set"))
            .ClickAsync(new MouseEventArgs());

        // AutoBuild returned Slam; the biggest remaining chart closes the session (§2.9), and
        // the button flips from an offer to a retune.
        Assert.Equal(2, page.FindAll(".mom-plan-sticker.on").Count);
        Assert.Contains("Rebuild the set", page.Markup);
    }

    [Fact]
    public async Task AManualTickTakesTheSetOffAutopilot()
    {
        var page = RenderComponent<Planner>();

        await page.FindAll("button").First(b => b.TextContent.Contains("Suggest a set"))
            .ClickAsync(new MouseEventArgs());
        Assert.Contains("Rebuild the set", page.Markup);

        await page.FindAll(".mom-plan-sticker")[0].ClickAsync(new MouseEventArgs());

        // Off autopilot: the button offers a fresh suggestion rather than a retune.
        Assert.Contains("Suggest a set", page.Markup);
        Assert.DoesNotContain("Rebuild the set", page.Markup);
    }

    private static RecordedPhoenixScore Record(Chart chart, int score)
    {
        return new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.FairGame, false,
            DateTimeOffset.UtcNow);
    }

    private static TournamentConfiguration NeutralConfiguration()
    {
        var scoring = new ScoringConfiguration
        {
            AdjustToTime = false,
            ContinuousLetterGradeScale = false,
            StageBreakModifier = 1.0,
            // The Doubles board zeroes every other chart type, exactly as a frozen MoM
            // configuration does — which is what keeps Singles records out of the pool.
            ChartTypeModifiers = Enum.GetValues<ChartType>()
                .ToDictionary(t => t, t => t == ChartType.Double ? 1.0 : 0.0)
        };
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
            scoring.LetterGradeModifiers[grade] = 1.0;
        foreach (var plate in Enum.GetValues<PhoenixPlate>())
            scoring.PlateModifiers[plate] = 1.0;
        scoring.MinimumScore = 500000;
        return new TournamentConfiguration(BoardId, "Summer 2026", scoring, false, true)
        {
            MaxTime = TimeSpan.FromMinutes(105)
        };
    }

    private static Chart BuildChart(string name, int level, ChartType type, TimeSpan duration)
    {
        var song = new Song(name, SongType.Arcade, new Uri("https://example.invalid/a.png"),
            duration, "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }
}
