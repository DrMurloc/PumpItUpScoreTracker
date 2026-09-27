using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The read behind board PUMBILITY peers, against a real database
///     (docs/design/pumbility-overhaul.md §6.13, §6.14). It joins one week's placements to the board
///     dimension and groups them per player and chart, and a mocked repository cannot catch a wrong
///     join or a filter that lets another week in — which is the whole reason these exist.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class BoardPeerReadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Week1 = new(2026, 8, 23, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week2 = new(2026, 8, 30, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week3 = new(2026, 9, 6, 16, 30, 0, TimeSpan.Zero);
    private static readonly Guid SinglesChart = Guid.NewGuid();
    private static readonly Guid OtherSinglesChart = Guid.NewGuid();
    private static readonly Guid DoublesChart = Guid.NewGuid();

    private readonly SqlServerFixture _fixture;

    public BoardPeerReadTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private EFOfficialSnapshotRepository Snapshots() => new(_fixture.DbContextFactory);

    /// <summary>
    ///     Two sealed weeks. Alice holds the singles chart in the first and is gone from it in the
    ///     second, the way a busy board pushes a player out; Bob improves.
    /// </summary>
    private async Task<Seeded> Seed()
    {
        var snapshots = Snapshots();
        var singles = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S23",
            SinglesChart, "Single", 23, CancellationToken.None);
        var other = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S12",
            OtherSinglesChart, "Single", 12, CancellationToken.None);
        var doubles = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart D23",
            DoublesChart, "Double", 23, CancellationToken.None);
        var pumbility = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Rating,
            PumbilityBoards.Singles, null, null, null, CancellationToken.None);

        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("ALICE#1111", (Uri?)null), ("BOB#2222", (Uri?)null) }, Week1, CancellationToken.None);
        var alice = players[0];
        var bob = players[1];

        var week1 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        await snapshots.WritePlacements(week1, new[]
        {
            new PlacementRow(singles.Id, alice.Id, 1, 995_000),
            new PlacementRow(singles.Id, bob.Id, 2, 980_000),
            new PlacementRow(other.Id, alice.Id, 1, 999_000),
            new PlacementRow(doubles.Id, alice.Id, 1, 991_000)
        }, CancellationToken.None);
        await snapshots.Seal(week1, Week1.AddMinutes(40), CancellationToken.None);

        var week2 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2, CancellationToken.None);
        await snapshots.WritePlacements(week2, new[]
        {
            // Alice's singles row is gone this week; Bob improved. The pool board is written here.
            new PlacementRow(singles.Id, bob.Id, 1, 986_000),
            new PlacementRow(pumbility.Id, alice.Id, 1, 19_100),
            new PlacementRow(pumbility.Id, bob.Id, 2, 18_800)
        }, CancellationToken.None);
        await snapshots.Seal(week2, Week2.AddMinutes(40), CancellationToken.None);

        return new Seeded(singles, alice, bob, week1, week2);
    }

    [Fact]
    public async Task AScoreGoneFromTheWeekIsGoneFromTheReading()
    {
        var seeded = await Seed();

        var rows = await Snapshots().GetChartScoresIn(seeded.Week2, PlacementScope.OfficialOnly,
            CancellationToken.None);

        // Alice's 995,000 from the week before does not follow her into this one, and the pool
        // board's rows are not chart scores.
        var row = Assert.Single(rows);
        Assert.Equal(seeded.Bob.Id, row.PlayerId);
        Assert.Equal(SinglesChart, row.ChartId);
        Assert.Equal(23, row.Level);
        Assert.Equal(ChartType.Single, row.Type);
        Assert.Equal(986_000, row.Score);
    }

    [Fact]
    public async Task EachWeekReadsItsOwnRowsAndNothingLater()
    {
        var seeded = await Seed();

        var rows = await Snapshots().GetChartScoresIn(seeded.Week1, PlacementScope.OfficialOnly,
            CancellationToken.None);

        Assert.Equal(4, rows.Count);
        // Bob's better score is a week later and stays there.
        Assert.Equal(980_000, rows.Single(r => r.PlayerId == seeded.Bob.Id).Score);
        var alice = rows.Where(r => r.PlayerId == seeded.Alice.Id).ToDictionary(r => r.ChartId);
        Assert.Equal(995_000, alice[SinglesChart].Score);
        Assert.Equal((12, ChartType.Single), (alice[OtherSinglesChart].Level, alice[OtherSinglesChart].Type));
        Assert.Equal((23, ChartType.Double), (alice[DoublesChart].Level, alice[DoublesChart].Type));
    }

    [Fact]
    public async Task ASupplementedRowStaysOutOfTheOfficialReading()
    {
        var seeded = await Seed();
        var snapshots = Snapshots();
        var carol = (await snapshots.EnsurePlayers(MixEnum.Phoenix2, new[] { ("CAROL#3333", (Uri?)null) },
            Week2, CancellationToken.None))[0];
        await snapshots.WritePlacements(seeded.Week2,
            new[] { new PlacementRow(seeded.Singles.Id, carol.Id, 2, 984_000, true) }, CancellationToken.None);

        var official = await snapshots.GetChartScoresIn(seeded.Week2, PlacementScope.OfficialOnly,
            CancellationToken.None);
        var supplemented = await snapshots.GetChartScoresIn(seeded.Week2, PlacementScope.IncludingSupplemented,
            CancellationToken.None);

        Assert.DoesNotContain(official, r => r.PlayerId == carol.Id);
        Assert.Equal(984_000, Assert.Single(supplemented, r => r.PlayerId == carol.Id).Score);
    }

    [Fact]
    public async Task CoOpAndTheLevelsNoPoolWouldPriceAreCarried()
    {
        // The board is held whole rather than trimmed to what a pool prices, because the same
        // read answers a chart dialog: a CO-OP chart has a board and no pool, and a level 4
        // singles chart has a board a fifty would never reach. Trimming either one turns a
        // board peer into silence on those pages.
        await Seed();
        var snapshots = Snapshots();
        var coopChart = Guid.NewGuid();
        var tinyChart = Guid.NewGuid();
        var coop = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart CoOp3",
            coopChart, "CoOp", 3, CancellationToken.None);
        var tiny = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S4",
            tinyChart, "Single", 4, CancellationToken.None);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("ALICE#1111", (Uri?)null) }, Week1, CancellationToken.None);
        var week3 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week3, CancellationToken.None);
        await snapshots.WritePlacements(week3, new[]
        {
            new PlacementRow(coop.Id, players[0].Id, 1, 993_000),
            new PlacementRow(tiny.Id, players[0].Id, 1, 1_000_000)
        }, CancellationToken.None);
        await snapshots.Seal(week3, Week3.AddMinutes(40), CancellationToken.None);

        var rows = await Snapshots().GetChartScoresIn(week3, PlacementScope.OfficialOnly, CancellationToken.None);

        var coopRow = Assert.Single(rows, r => r.ChartId == coopChart);
        Assert.Equal((ChartType.CoOp, 993_000), (coopRow.Type, coopRow.Score));
        Assert.Equal(1_000_000, Assert.Single(rows, r => r.ChartId == tinyChart).Score);
    }

    [Fact]
    public async Task TheLatestSealedWeekIsNeverARunStillInFlight()
    {
        var seeded = await Seed();
        var snapshots = Snapshots();
        // A third sweep has started and written Alice back onto the chart, but it has not sealed.
        var inFlight = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week3, CancellationToken.None);
        await snapshots.WritePlacements(inFlight,
            new[] { new PlacementRow(seeded.Singles.Id, seeded.Alice.Id, 1, 999_000) }, CancellationToken.None);

        var latest = await snapshots.GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None);
        var rows = await new BoardScoreStore(snapshots).InLevelRange(MixEnum.Phoenix2, latest!.Id,
            ChartType.Single, new[] { seeded.Alice.Id, seeded.Bob.Id }, 20, 29, CancellationToken.None);

        Assert.Equal(seeded.Week2, latest.Id);

        var row = Assert.Single(rows);
        Assert.Equal((seeded.Bob.Id, 986_000), (row.PlayerId, row.Score));
    }

    [Fact]
    public async Task ThePoolBoardCarriesEveryPlayersPublishedPool()
    {
        var seeded = await Seed();
        var snapshots = Snapshots();
        var latest = await snapshots.GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None);
        var board = (await snapshots.GetBoards(MixEnum.Phoenix2, CancellationToken.None))
            .Single(b => b.LeaderboardType == LeaderboardTypes.Rating && b.Name == PumbilityBoards.Singles);

        var rows = await snapshots.GetBoardPlacements(latest!.Id, board.Id, PlacementScope.OfficialOnly,
            CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(19_100, rows.Single(r => r.PlayerId == seeded.Alice.Id).Score);
        Assert.Equal(18_800, rows.Single(r => r.PlayerId == seeded.Bob.Id).Score);
    }

    private sealed record Seeded(BoardDimension Singles, PlayerDimension Alice, PlayerDimension Bob, int Week1,
        int Week2);
}
