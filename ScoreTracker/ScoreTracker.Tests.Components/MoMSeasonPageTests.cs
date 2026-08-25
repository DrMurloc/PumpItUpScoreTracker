using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.MarchOfMurlocs;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Season page (march-of-murlocs.md §11.2) as static markup: rank order, the
///     session-number chips (D16), viewer highlighting, the standing cards, the D12
///     Phoenix 2 explainer, and the ended-season state with no record affordances.
/// </summary>
public sealed class MoMSeasonPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DoublesBoard = Guid.NewGuid();
    private static readonly Guid SinglesBoard = Guid.NewGuid();

    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public MoMSeasonPageTests()
    {
        _uiSettings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == Now));
        Services.AddSingleton(Mock.Of<IUserRepository>());
        Services.AddScoped<CommunityGlowReader>();

        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MoMSeasonListing>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<CommunityOverviewRecord>());

        // Last: touching Renderer freezes the service provider, so every registration above
        // must precede it. The Season page is static SSR (RenderModeDeclarationTests pins
        // it), so the tests render it in the static world — which is also what UserLabel
        // keys its tooltips off.
        Renderer.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Static", false));
    }

    [Fact]
    public void RendersBoardRowsInRankOrderWithSessionChips()
    {
        var repeat = Guid.NewGuid();
        WithSeason(live: true);
        WithBoardRows(DoublesBoard,
            Row(1, repeat, "tieny", 52979, Now.AddDays(-16)),
            Row(2, repeat, "tieny", 49757, Now.AddDays(-2)),
            Row(3, Guid.NewGuid(), "Redviper", 47940, Now.AddDays(-8)));
        WithBoardRows(SinglesBoard);

        var page = RenderComponent<Season>();

        var rows = page.FindAll(".mom-row");
        Assert.Equal(3, rows.Count);
        Assert.Contains("tieny", rows[0].TextContent);
        Assert.Contains("52,979", rows[0].TextContent);
        // D16: the same player's later-published session wears the ordinal chip.
        Assert.Empty(rows[0].GetElementsByClassName("mom-runchip"));
        Assert.Contains("session #2", rows[1].TextContent);
        // The countdown renders for a live season, and the type pair are links.
        Assert.Contains("days left", page.Markup);
        var group = page.Find(".mom-typegroup");
        Assert.Equal(2, group.Children.Length);
        Assert.Contains("/MarchOfMurlocs/2026/Summer/Doubles",
            group.Children[0].GetAttribute("href"));
    }

    [Fact]
    public void ViewerRowsWearTheYouClassAndTheStandingCardTheirBest()
    {
        var me = Guid.NewGuid();
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(me, Name.From("Me"), true, null,
            new Uri("https://example.invalid/a.png"), null));
        WithSeason(live: true);
        WithBoardRows(DoublesBoard,
            Row(1, Guid.NewGuid(), "Winner", 60000, Now.AddDays(-10)),
            Row(2, me, "Me", 50000, Now.AddDays(-9)),
            Row(3, me, "Me", 40000, Now.AddDays(-8)));
        WithBoardRows(SinglesBoard);

        var page = RenderComponent<Season>();

        Assert.Equal(2, page.FindAll(".mom-row.mom-you").Count);
        var entered = page.Find(".mom-stand.entered");
        Assert.Contains("50,000", entered.TextContent);
        Assert.Contains("your best of 2 sessions", entered.TextContent);
        // The Singles card is the empty CTA, and doubles as the switch to that board.
        var empty = page.Find(".mom-stand.empty");
        Assert.Contains("Record your first session", empty.TextContent);
        Assert.Contains("/MarchOfMurlocs/2026/Summer/Singles", empty.GetAttribute("href"));
    }

    [Fact]
    public void Phoenix2ViewerSeesTheExplainerInsteadOfBoards()
    {
        _uiSettings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        WithSeason(live: true);

        var page = RenderComponent<Season>();

        Assert.Contains("Phoenix 2 scoring is still being tuned.", page.Markup);
        Assert.Empty(page.FindAll(".mom-row"));
        Assert.Empty(page.FindAll(".mom-typegroup"));
    }

    [Fact]
    public void EndedSeasonRendersNoRecordAffordancesAndNoCountdown()
    {
        WithSeason(live: false);
        WithBoardRows(DoublesBoard, Row(1, Guid.NewGuid(), "Winner", 60000, Now.AddDays(-100)));
        WithBoardRows(SinglesBoard);

        var page = RenderComponent<Season>();

        Assert.DoesNotContain("Record a session", page.Markup);
        Assert.DoesNotContain("days left", page.Markup);
        Assert.Contains("you sat this one out", page.Find(".mom-stand.empty").TextContent);
        // The archive stays crawlable through the season's own neighbours (§11.8).
        Assert.Contains("Spring 2026", page.Find(".mom-prevnext").TextContent);
    }

    private void WithSeason(bool live)
    {
        var start = live ? Now.AddDays(-55) : Now.AddDays(-150);
        var end = live ? Now.AddDays(36) : Now.AddDays(-60);
        var season = new MoMSeasonView(Guid.NewGuid(), "Summer 2026", 2026, 3, start, end, live,
            new[]
            {
                new MoMBoardSummary(DoublesBoard, MixEnum.Phoenix, ChartType.Double, 3),
                new MoMBoardSummary(SinglesBoard, MixEnum.Phoenix, ChartType.Single, 0)
            },
            new MoMSeasonRef(Guid.NewGuid(), "Spring 2026", 2026, 2), null);
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(season);
    }

    private void WithBoardRows(Guid boardId, params MoMBoardRow[] rows)
    {
        var type = boardId == DoublesBoard ? ChartType.Double : ChartType.Single;
        var view = new MoMBoardView(boardId, new MoMSeasonRef(Guid.NewGuid(), "Summer 2026", 2026, 3),
            MixEnum.Phoenix, type, rows);
        Mediator.Setup(m => m.Send(
                It.Is<GetMoMBoardQuery>(q => q.BoardId == boardId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
    }

    private static MoMBoardRow Row(int place, Guid userId, string name, int total,
        DateTimeOffset publishedAt)
    {
        return new MoMBoardRow(place, Guid.NewGuid(), userId, name,
            new Uri("https://example.invalid/avatar.png"), null, total, 30, 23.5, 10, 20, 22,
            TimeSpan.FromMinutes(20), publishedAt, null);
    }
}
