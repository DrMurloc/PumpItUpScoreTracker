using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The two reads behind board PUMBILITY peers, against a real database
///     (docs/design/pumbility-overhaul.md §6.13). Both join placements to the board dimension and
///     collapse across snapshots, and a mocked repository cannot catch a wrong join or a group-by
///     that silently keeps the wrong row — which is the whole reason these exist.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class BoardPeerReadTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Week1 = new(2026, 8, 23, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week2 = new(2026, 8, 30, 16, 30, 0, TimeSpan.Zero);
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
    ///     Two sealed weeks. A player's score on the singles chart FALLS between them, which is the
    ///     case the across-snapshot read exists for: dropping off a crowding board is not evidence
    ///     a score went away.
    /// </summary>
    private async Task<(BoardDimension Singles, BoardDimension Other, BoardDimension Doubles,
        PlayerDimension Alice, PlayerDimension Bob)> Seed()
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

        return (singles, other, doubles, alice, bob);
    }

    [Fact]
    public async Task TheBestScoreAcrossEveryWeekIsTheReadingEvenWhenTheRowIsGone()
    {
        var seeded = await Seed();

        var rows = await Snapshots().GetChartHistoryFor(MixEnum.Phoenix2,
            new[] { seeded.Alice.Id, seeded.Bob.Id }, ChartType.Single, 20, 29,
            PlacementScope.OfficialOnly, CancellationToken.None);

        var alice = Assert.Single(rows, r => r.PlayerId == seeded.Alice.Id);
        Assert.Equal(995_000, alice.Score);
        Assert.Equal(SinglesChart, alice.ChartId);
        Assert.Equal(23, alice.Level);
        // Bob's two rows collapse to his better one rather than doubling him.
        var bob = Assert.Single(rows, r => r.PlayerId == seeded.Bob.Id);
        Assert.Equal(986_000, bob.Score);
    }

    [Fact]
    public async Task TheLevelBandAndTheChartTypeBothBound()
    {
        var seeded = await Seed();
        var snapshots = Snapshots();

        var inBand = await snapshots.GetChartHistoryFor(MixEnum.Phoenix2, new[] { seeded.Alice.Id },
            ChartType.Single, 20, 29, PlacementScope.OfficialOnly, CancellationToken.None);
        var wholePool = await snapshots.GetChartHistoryFor(MixEnum.Phoenix2, new[] { seeded.Alice.Id },
            ChartType.Single, 10, 29, PlacementScope.OfficialOnly, CancellationToken.None);
        var doubles = await snapshots.GetChartHistoryFor(MixEnum.Phoenix2, new[] { seeded.Alice.Id },
            ChartType.Double, 10, 29, PlacementScope.OfficialOnly, CancellationToken.None);

        // The level 12 chart is hers too, and only the wider read carries it.
        Assert.Single(inBand);
        Assert.Equal(2, wholePool.Count);
        Assert.Equal(DoublesChart, Assert.Single(doubles).ChartId);
    }

    [Fact]
    public async Task TheChartBoundReadCarriesCoOpAndTheLevelsNoPoolWouldPrice()
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
        var week3 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2.AddDays(7),
            CancellationToken.None);
        await snapshots.WritePlacements(week3, new[]
        {
            new PlacementRow(coop.Id, players[0].Id, 1, 993_000),
            new PlacementRow(tiny.Id, players[0].Id, 1, 1_000_000)
        }, CancellationToken.None);
        await snapshots.Seal(week3, Week2.AddDays(7).AddMinutes(40), CancellationToken.None);

        var rows = await Snapshots().GetChartHistoryOn(MixEnum.Phoenix2, new[] { players[0].Id },
            new[] { coopChart, tinyChart }, PlacementScope.OfficialOnly, CancellationToken.None);

        Assert.Equal(993_000, Assert.Single(rows, r => r.ChartId == coopChart).Score);
        Assert.Equal(1_000_000, Assert.Single(rows, r => r.ChartId == tinyChart).Score);
    }

    [Fact]
    public async Task TheChartBoundReadAnswersOnlyTheChartsAsked()
    {
        var seeded = await Seed();

        var rows = await Snapshots().GetChartHistoryOn(MixEnum.Phoenix2,
            new[] { seeded.Alice.Id }, new[] { SinglesChart }, PlacementScope.OfficialOnly,
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(SinglesChart, row.ChartId);
        Assert.Equal(995_000, row.Score);
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

    [Fact]
    public async Task AnEmptyPlayerSetReadsNothingRatherThanEverything()
    {
        await Seed();

        Assert.Empty(await Snapshots().GetChartHistoryFor(MixEnum.Phoenix2, Array.Empty<int>(),
            ChartType.Single, 10, 29, PlacementScope.OfficialOnly, CancellationToken.None));
        Assert.Empty(await Snapshots().GetChartHistoryOn(MixEnum.Phoenix2, Array.Empty<int>(),
            new[] { SinglesChart }, PlacementScope.OfficialOnly, CancellationToken.None));
    }
}
