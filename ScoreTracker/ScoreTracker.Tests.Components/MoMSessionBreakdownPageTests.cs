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
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.MarchOfMurlocs;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Session Breakdown (march-of-murlocs.md §11.3): the four levers ranked against the
///     board, the same-board and cross-season compare — the latter carrying the D20
///     re-rating ledger and pricing both h2h sides under this season — and the D21 rule that
///     Compact never sorts by a number the sticker does not print.
/// </summary>
public sealed class MoMSessionBreakdownPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Published = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid RivalSessionId = Guid.NewGuid();
    private static readonly Guid PriorSessionId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly MoMSeasonRef Season = new(Guid.NewGuid(), "Winter 2025", 2025, 1);
    private static readonly MoMSeasonRef Prior = new(Guid.NewGuid(), "March of Murlocs 2", null, null);

    private readonly Chart _chartA;
    private readonly Chart _chartB;
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public MoMSessionBreakdownPageTests()
    {
        _chartA = BuildChart("Gargoyle", 25);
        _chartB = BuildChart("Slam", 23);

        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>())).ReturnsAsync((string?)null);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Published.AddDays(5)));
        Services.AddScoped<ChartCatalogCache>();
        Services.AddScoped<CommunityGlowReader>();

        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _chartA, _chartB });
        Mediator.Setup(m => m.Send(It.Is<GetMoMSessionQuery>(q => q.SessionId == SessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(View(SessionId, "김재현", 59319, place: 1,
                Row(0, _chartA, 844710, 3207), Row(1, _chartB, 962566, 2990)));
        Mediator.Setup(m => m.Send(It.Is<GetMoMSessionQuery>(q => q.SessionId == RivalSessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(View(RivalSessionId, "yimmythe42", 57325, place: 2,
                Row(0, _chartA, 924890, 5099)));
        Mediator.Setup(m => m.Send(It.Is<GetMoMSessionQuery>(q => q.SessionId == PriorSessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(View(PriorSessionId, "김재현", 44139, place: 10,
                Row(0, _chartA, 840000, 1500)) with { Season = Prior });
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMBoardView(BoardId, Season, MixEnum.Phoenix, ChartType.Double,
                new[]
                {
                    BoardRow(SessionId, "김재현", 1, 59319, 39, 24.22, 11.2),
                    BoardRow(RivalSessionId, "yimmythe42", 2, 57325, 36, 23.96, 11.6)
                }));
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MoMSeasonListing(Prior, Published.AddYears(-1), Published.AddMonths(-10),
                    false, new[]
                    {
                        new MoMBoardStanding(Guid.NewGuid(), MixEnum.Phoenix, ChartType.Double,
                            17, "FEFEMZ", 78691, 10, 44139, PriorSessionId)
                    })
            });
        Mediator.Setup(m => m.Send(It.IsAny<RepriceMoMSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionReprice(44139, 47923, 10, 810, 2449,
                new Dictionary<Guid, int> { [_chartA.Id] = 1892 }));

        Renderer.SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void RendersTheFourLeversRankedAgainstTheBoard()
    {
        var page = Render();

        var levers = page.FindAll(".mom-lever");
        Assert.Equal(4, levers.Count);
        Assert.Contains("Most on this board", levers[0].TextContent);
        Assert.Contains("39", levers[0].TextContent);
        Assert.Contains("balanced for this season", levers[1].TextContent);
        Assert.Contains("59,319", page.Find(".mom-totalbox").TextContent);
    }

    [Fact]
    public void BoardCompareListsCommonChartsWorstFirst()
    {
        var page = Render();

        var verdict = page.Find(".cmp-verdict").TextContent;
        Assert.Contains("+1,994", verdict);
        var h2h = page.Find(".mom-h2h");
        Assert.Contains("1 in common", h2h.TextContent);
        // Gargoyle cost 3,207 − 5,099 = −1,892 — the widest gap leads and is named.
        Assert.Contains("Gargoyle alone cost you 1,892 points", h2h.TextContent.Replace("  ", " "));
    }

    [Fact]
    public async Task SeasonCompareShowsTheReratingLedgerAndRepricesTheirSide()
    {
        var page = Render();

        await page.FindAll(".mom-typegroup a")[1].ClickAsync(new MouseEventArgs());

        var ledger = page.Find(".mom-ledger");
        Assert.Contains("What changed underneath you", ledger.TextContent);
        Assert.Contains("+810", ledger.TextContent);
        Assert.Contains("+2,449", ledger.TextContent);
        Assert.Contains("47,923", ledger.TextContent);
        // The h2h prices the old session's Gargoyle under THIS season: 1,892, not 1,500.
        Assert.Contains("1,892", page.Find(".mom-h2h").TextContent);
    }

    [Fact]
    public async Task CompactCornerCarriesTheSortValueWhenItIsNotThePrintedOne()
    {
        var page = Render();

        // The printed value is the MoM score (D21) — Gargoyle's 3,207 points.
        Assert.Contains("3,207", page.FindAll(".tier-chart-card-corner")[0].TextContent);

        // Sort by PPS from the Table headers (the popover and the headers drive one state),
        // then return to Compact: the invisible sort value joins the corner.
        await page.FindAll(".mom-runbar .mud-icon-button")[2].ClickAsync(new MouseEventArgs());
        await page.FindAll(".mom-runtable th button")[3].ClickAsync(new MouseEventArgs());
        await page.FindAll(".mom-runbar .mud-icon-button")[1].ClickAsync(new MouseEventArgs());

        Assert.Contains("/s", page.FindAll(".tier-chart-card-corner")[0].TextContent);
    }

    [Fact]
    public async Task DensityChoicePersistsThroughTheSettingKey()
    {
        var page = Render();

        await page.FindAll(".mom-runbar .mud-icon-button")[2].ClickAsync(new MouseEventArgs());

        Assert.NotNull(page.Find(".mom-runtable"));
        _uiSettings.Verify(u => u.SetSetting("Density__MoMSession", "Table"), Times.Once);
    }

    private IRenderedComponent<SessionBreakdown> Render()
    {
        return RenderComponent<SessionBreakdown>(p => p.Add(x => x.Id, SessionId));
    }

    private static MoMSessionView View(Guid id, string name, int total, int? place,
        params MoMSessionChartRow[] charts)
    {
        // ChartsPlayed mirrors the board row (39), not the two sampled chart rows — the
        // levers read the derived cache, and the board is where the rank comes from.
        return new MoMSessionView(id, BoardId, Season, MixEnum.Phoenix, ChartType.Double,
            Guid.NewGuid(), name, Published, total, 39,
            TimeSpan.FromMinutes(22), 24.22, 11.2, 21, 26, null, place,
            TimeSpan.FromMinutes(105), false, charts);
    }

    private static MoMSessionChartRow Row(int ordinal, Chart chart, int score, int points)
    {
        return new MoMSessionChartRow(ordinal, chart.Id, score, PhoenixPlate.RoughGame, false,
            points, 0, null, (int)chart.Level + 0.5);
    }

    private static MoMBoardRow BoardRow(Guid sessionId, string name, int place, int total,
        int charts, double difficulty, double grade)
    {
        return new MoMBoardRow(place, sessionId, Guid.NewGuid(), name, null, null, total, charts,
            difficulty, grade, 21, 26, TimeSpan.FromMinutes(22), Published, null);
    }

    private static Chart BuildChart(string name, int level)
    {
        var song = new Song(name, SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Double,
            DifficultyLevel.From(level), MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }
}
