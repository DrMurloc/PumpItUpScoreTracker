using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Season page, the seasons dialog and the legacy locator (docs/design/march-of-murlocs.md
///     §11.2, §11.8): the live season leads, boards rank sessions Doubles first, the viewer's
///     standing is their best session, and a past season carries its neighbours.
/// </summary>
public sealed class MoMReadHandlerTests
{
    private static readonly DateTimeOffset Feb = new(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static (MoMReadHandlerFixture F, Guid Winter, Guid Kim, Guid Tieny) Board()
    {
        var f = new MoMReadHandlerFixture();
        var mom2 = f.Season("March of Murlocs 2", Feb.AddMonths(-8), Feb.AddMonths(-6));
        var winter = f.Season("Winter 2025", Feb, Feb.AddMonths(2));
        var doubles = f.Board(winter, ChartType.Double);
        f.Board(winter, ChartType.Single);
        f.Board(mom2, ChartType.Double);
        var kim = f.User("김재현");
        var tieny = f.User("tieny");
        var other = f.User("someone");
        f.Session(doubles, kim, 59319, Feb.AddDays(13), charts: 39, averageDifficulty: 24.22, downtime: TimeSpan.FromSeconds(1324));
        f.Session(doubles, tieny, 52979, Feb.AddDays(3));
        f.Session(doubles, other, 52979, Feb.AddDays(1));
        f.Session(doubles, tieny, 41780, Feb.AddDays(20));
        f.Session(doubles, other, 70000, null); // a draft never reaches the board
        return (f, winter.Id, kim.Id, tieny.Id);
    }

    [Fact]
    public async Task TheLiveSeasonLeadsWithDoublesFirstAndSessionsRankedInScoreOrder()
    {
        var (f, winter, kim, _) = Board();

        var page = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(winter, page!.Season.Id);
        Assert.True(page.Season.IsLive);
        Assert.Equal(new[] { ChartType.Double, ChartType.Single }, page.Boards.Select(b => b.ChartType));
        var rows = page.Boards[0].Rows;
        Assert.Equal(4, rows.Count);
        Assert.Equal(kim, rows[0].UserId);
        Assert.Equal("김재현", rows[0].Player!.Name.ToString());
        Assert.Equal(1, rows[0].Place);
        Assert.Equal(Feb.AddDays(1), rows[1].PublishedAt); // the earlier 52,979 wins the tie
        Assert.Equal(3, rows[2].Place);
        Assert.Equal(2, rows[3].SessionNumber); // tieny's second session on the board
        Assert.Equal(MoMReadHandlerFixture.Window, page.Boards[0].Window);
        Assert.Empty(page.Boards[1].Rows);
        Assert.Equal("March of Murlocs 2", page.Previous!.Name);
        Assert.Null(page.Next);
    }

    [Fact]
    public async Task TheViewersStandingIsTheirBestSessionAndHowManyTheyPlayed()
    {
        var (f, _, kim, tieny) = Board();

        var page = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix, ViewerId: tieny), CancellationToken.None);
        var anonymous = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix), CancellationToken.None);
        var stranger = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix, ViewerId: Guid.NewGuid()), CancellationToken.None);

        var standing = page!.Boards[0].Viewer;
        Assert.NotNull(standing);
        Assert.Equal(3, standing!.Place);
        Assert.Equal(4, standing.Of);
        Assert.Equal(52979, standing.TotalScore);
        Assert.Equal(2, standing.SessionCount);
        Assert.Null(page.Boards[1].Viewer);
        Assert.Null(anonymous!.Boards[0].Viewer);
        Assert.Null(stranger!.Boards[0].Viewer);
        Assert.Equal(1, (await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix, ViewerId: kim), CancellationToken.None))!.Boards[0].Viewer!.Place);
    }

    [Fact]
    public async Task PhoenixTwoHasTheSeasonButNoBoardsYet()
    {
        var (f, winter, _, _) = Board();
        var page = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix2), CancellationToken.None);
        Assert.Equal(winter, page!.Season.Id);
        Assert.Empty(page.Boards);
    }

    [Fact]
    public async Task APastSeasonByIdCarriesItsNeighboursAndIsNotLive()
    {
        var (f, _, _, _) = Board();
        var mom2 = f.Seasons.Single(s => s.Name == "March of Murlocs 2");
        var practice = f.Season("Practice", Feb.AddMonths(-16), Feb.AddMonths(-15));

        var page = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix, mom2.Id), CancellationToken.None);

        Assert.False(page!.Season.IsLive);
        Assert.Equal(practice.Id, page.Previous!.Id);
        Assert.Equal("Winter 2025", page.Next!.Name);
        Assert.Single(page.Boards); // MoM 2 ran Doubles only
    }

    [Fact]
    public async Task BetweenSeasonsTheMostRecentOneStandsIn()
    {
        // The clock sits after every season's end and before the next cycle's tick: there is
        // still always a season on the landing page — the latest one.
        var f = new MoMReadHandlerFixture();
        f.Season("Old", MoMReadHandlerFixture.Now.AddMonths(-9), MoMReadHandlerFixture.Now.AddMonths(-6));
        var latest = f.Season("Latest", MoMReadHandlerFixture.Now.AddMonths(-3), MoMReadHandlerFixture.Now.AddDays(-1));

        var page = await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(latest.Id, page!.Season.Id);
        Assert.False(page.Season.IsLive);
    }

    [Fact]
    public async Task NoSeasonAtAllIsNull()
    {
        var f = new MoMReadHandlerFixture();
        Assert.Null(await f.Handler().Handle(new GetMoMSeasonPageQuery(MixEnum.Phoenix), CancellationToken.None));
    }

    [Fact]
    public async Task TheLocatorNamesTheSeasonAndTypeBehindABoardId()
    {
        var (f, winter, _, _) = Board();
        var singles = f.Boards.Single(b => b.SeasonId == winter && b.ChartType == ChartType.Single);

        var locator = await f.Handler().Handle(new GetMoMBoardLocatorQuery(singles.Id), CancellationToken.None);

        Assert.Equal(winter, locator!.SeasonId);
        Assert.Equal(ChartType.Single, locator.ChartType);
        Assert.True(locator.IsLive);
        Assert.Null(await f.Handler().Handle(new GetMoMBoardLocatorQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task TheSeasonsDialogListsEverySeasonNewestFirstWithWinnersAndTheViewersPlace()
    {
        var (f, _, kim, tieny) = Board();

        var listing = await f.Handler().Handle(new GetMoMSeasonsQuery(MixEnum.Phoenix, tieny), CancellationToken.None);

        Assert.Equal(new[] { "Winter 2025", "March of Murlocs 2" }, listing.Select(l => l.Season.Name));
        var doubles = listing[0].Boards[0];
        Assert.Equal(ChartType.Double, doubles.ChartType);
        Assert.Equal(4, doubles.SessionCount);
        Assert.Equal(kim, doubles.Winner!.Id);
        Assert.Equal(59319, doubles.WinningScore);
        Assert.Equal(3, doubles.ViewerPlace);
        Assert.Equal(52979, doubles.ViewerScore);
        var singles = listing[0].Boards[1];
        Assert.Equal(0, singles.SessionCount);
        Assert.Null(singles.Winner);
        Assert.Null(singles.ViewerPlace);
        Assert.Single(listing[1].Boards);
    }
}
