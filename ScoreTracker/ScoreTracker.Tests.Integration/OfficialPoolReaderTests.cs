using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The board players' pools the Hardmode census counts, against a real database
///     (docs/design/hardmode-leaderboard.md §2, D35). The read joins one week's placements to the
///     board dimension and the catalog, and a mocked repository cannot catch a filter that lets
///     another week in.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class OfficialPoolReaderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Week1 = new(2026, 8, 23, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week2 = new(2026, 8, 30, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week3 = new(2026, 9, 6, 16, 30, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public OfficialPoolReaderTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private EFOfficialSnapshotRepository Snapshots() => new(_fixture.DbContextFactory);

    private OfficialPoolReader Reader() => new(Snapshots(), _fixture.DbContextFactory);

    /// <summary>A Phoenix 2 catalog chart, so the read's join to the mix's own level finds it.</summary>
    private async Task<Guid> Phoenix2Chart(int level)
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var mixId = MixIds.For(MixEnum.Phoenix2);
        await seeder.EnsureMixAsync(mixId);
        var chartId = await seeder.SeedChartAsync(level);
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        database.ChartMix.Add(new ChartMixEntity { Id = Guid.NewGuid(), ChartId = chartId, MixId = mixId, Level = level });
        await database.SaveChangesAsync();
        return chartId;
    }

    private static double Priced(int level, int score)
    {
        var phoenixScore = PhoenixScore.From(score);
        return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false).GetScore(ChartType.Single,
            DifficultyLevel.From(level), phoenixScore, ScoringConfiguration.ExpectedPlateForScore(phoenixScore));
    }

    [Fact]
    public async Task BoardPlayersArePricedFromTheLatestSealedWeekAlone()
    {
        var s21 = await Phoenix2Chart(21);
        var s22 = await Phoenix2Chart(22);
        var snapshots = Snapshots();
        var board21 = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S21", s21,
            "Single", 21, CancellationToken.None);
        var board22 = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S22", s22,
            "Single", 22, CancellationToken.None);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("ALICE#1111", (Uri?)null), ("BOB#2222", (Uri?)null) }, Week1, CancellationToken.None);
        var (alice, bob) = (players[0], players[1]);

        var week1 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        await snapshots.WritePlacements(week1, new[]
        {
            new PlacementRow(board21.Id, alice.Id, 1, 995_000),
            new PlacementRow(board21.Id, bob.Id, 2, 980_000),
            new PlacementRow(board22.Id, alice.Id, 1, 990_000)
        }, CancellationToken.None);
        await snapshots.Seal(week1, Week1.AddMinutes(40), CancellationToken.None);

        // Alice has been pushed off S21 since; Bob improved on it.
        var week2 = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2, CancellationToken.None);
        await snapshots.WritePlacements(week2, new[]
        {
            new PlacementRow(board21.Id, bob.Id, 1, 986_000),
            new PlacementRow(board22.Id, alice.Id, 1, 992_000)
        }, CancellationToken.None);
        await snapshots.Seal(week2, Week2.AddMinutes(40), CancellationToken.None);

        // A third sweep is under way and has written Alice back onto S21, but it has not sealed.
        var inFlight = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week3, CancellationToken.None);
        await snapshots.WritePlacements(inFlight, new[] { new PlacementRow(board21.Id, alice.Id, 1, 999_000) },
            CancellationToken.None);

        var pools = await Reader().GetPricedPools(MixEnum.Phoenix2, CancellationToken.None);

        var alicePool = Assert.Single(pools, p => p.OfficialPlayerId == alice.Id);
        var aliceChart = Assert.Single(alicePool.Charts);
        Assert.Equal((s22, Priced(22, 992_000)), (aliceChart.ChartId, aliceChart.Value));
        var bobChart = Assert.Single(Assert.Single(pools, p => p.OfficialPlayerId == bob.Id).Charts);
        Assert.Equal((s21, Priced(21, 986_000)), (bobChart.ChartId, bobChart.Value));
    }

    [Fact]
    public async Task APlayerLinkedToAnAccountIsLeftToTheirOwnRecords()
    {
        var s21 = await Phoenix2Chart(21);
        var snapshots = Snapshots();
        var board = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S21", s21,
            "Single", 21, CancellationToken.None);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("ALICE#1111", (Uri?)null), ("CAROL#3333", (Uri?)null) }, Week1, CancellationToken.None);
        var (alice, carol) = (players[0], players[1]);
        var account = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();
        await using (var database = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            await database.Set<OfficialPlayerEntity>().Where(p => p.Id == carol.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.UserId, (Guid?)account));
        }

        var week = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        await snapshots.WritePlacements(week, new[]
        {
            new PlacementRow(board.Id, alice.Id, 1, 995_000),
            new PlacementRow(board.Id, carol.Id, 2, 990_000)
        }, CancellationToken.None);
        await snapshots.Seal(week, Week1.AddMinutes(40), CancellationToken.None);

        var pools = await Reader().GetPricedPools(MixEnum.Phoenix2, CancellationToken.None);

        Assert.Equal(alice.Id, Assert.Single(pools).OfficialPlayerId);
    }

    [Fact]
    public async Task AMixThatHasNeverSealedAWeekHasNoBoardPlayers()
    {
        var s21 = await Phoenix2Chart(21);
        var snapshots = Snapshots();
        var board = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Chart S21", s21,
            "Single", 21, CancellationToken.None);
        var alice = (await snapshots.EnsurePlayers(MixEnum.Phoenix2, new[] { ("ALICE#1111", (Uri?)null) }, Week1,
            CancellationToken.None))[0];
        var inFlight = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        await snapshots.WritePlacements(inFlight, new[] { new PlacementRow(board.Id, alice.Id, 1, 995_000) },
            CancellationToken.None);

        Assert.Empty(await Reader().GetPricedPools(MixEnum.Phoenix2, CancellationToken.None));
    }
}
