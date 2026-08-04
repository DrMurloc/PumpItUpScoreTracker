using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The leak test, against real SQL. A snapshot holds two readings in one table, and the
///     whole design rests on official reads never seeing the supplemented half — a miss there
///     invents world firsts, moves the tier lists and announces both on Discord. Mocked
///     repositories cannot catch it, because the predicate under test is the one they replace.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class SupplementedPlacementTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Week1 = new(2026, 8, 2, 16, 30, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;

    public SupplementedPlacementTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFOfficialSnapshotRepository Snapshots() => new(_fixture.DbContextFactory);
    private EFOfficialRecordRepository Records() => new(_fixture.DbContextFactory);
    private EFAccountPurgeRepository Purge() => new(_fixture.DbContextFactory);

    private sealed record Seeded(int SnapshotId, BoardDimension Board, PlayerDimension Official,
        PlayerDimension Ours);

    /// <summary>One official row and one supplemented row below it, on one sealed board.</summary>
    private async Task<Seeded> SeedBothReadings(Guid? ourUserId = null)
    {
        var snapshots = Snapshots();
        var ct = CancellationToken.None;
        var snapshotId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, ct);
        var board = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Bee S17",
            Guid.NewGuid(), "Single", 17, ct);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("OFFICIAL#1", (Uri?)null), ("OURS#2", (Uri?)null) }, Week1, ct);

        await snapshots.WritePlacements(snapshotId, new[]
        {
            new PlacementRow(board.Id, players[0].Id, 1, 990_000),
            new PlacementRow(board.Id, players[1].Id, 2, 970_000, true)
        }, ct);
        await snapshots.Seal(snapshotId, Week1.AddMinutes(40), ct);

        if (ourUserId != null)
            await new EFOfficialPlayerIdentityRepository(_fixture.DbContextFactory)
                .LinkPlayer(MixEnum.Phoenix2, "OURS#2", ourUserId.Value, Week1, ct);

        return new Seeded(snapshotId, board, players[0], players[1]);
    }

    [Fact]
    public async Task EveryOfficialReadPathIgnoresSupplementedRows()
    {
        var s = await SeedBothReadings();
        var snapshots = Snapshots();
        var ct = CancellationToken.None;
        const PlacementScope official = PlacementScope.OfficialOnly;

        Assert.Single(await snapshots.GetPlacements(s.SnapshotId, official, ct));
        Assert.Single(await snapshots.GetBoardPlacements(s.SnapshotId, s.Board.Id, official, ct));
        Assert.Single(await snapshots.GetBoardPlacements(s.SnapshotId, new[] { s.Board.Id }, official, ct));
        Assert.Single(await snapshots.GetPlacementDetails(s.SnapshotId, official, ct));
        Assert.Single(await snapshots.GetPlayerTimeline(s.Official.Id, official, ct));
        Assert.Empty(await snapshots.GetPlayerTimeline(s.Ours.Id, official, ct));

        // The debut seen-set: a supplemented player must not read as "already seen" to the
        // official reading, or their real first placement would never register as a debut.
        Assert.DoesNotContain(s.Ours.Id,
            await snapshots.GetSeenPlayerIds(MixEnum.Phoenix2, s.SnapshotId + 1, official, ct));

        Assert.Single(await snapshots.GetPlayerNames(MixEnum.Phoenix2, official, ct));
    }

    [Fact]
    public async Task TheSupplementedReadingSeesBoth()
    {
        var s = await SeedBothReadings();
        var snapshots = Snapshots();
        var ct = CancellationToken.None;
        const PlacementScope both = PlacementScope.IncludingSupplemented;

        Assert.Equal(2, (await snapshots.GetPlacements(s.SnapshotId, both, ct)).Count);
        Assert.Equal(2, (await snapshots.GetBoardPlacements(s.SnapshotId, s.Board.Id, both, ct)).Count);
        Assert.Equal(2, (await snapshots.GetPlacementDetails(s.SnapshotId, both, ct)).Count);
        Assert.Equal(2, (await snapshots.GetPlayerNames(MixEnum.Phoenix2, both, ct)).Count);
        Assert.Single(await snapshots.GetPlayerTimeline(s.Ours.Id, both, ct));
    }

    [Fact]
    public async Task TheReadingFlagSurvivesTheRoundTrip()
    {
        var s = await SeedBothReadings();

        var rows = await Snapshots().GetPlacements(s.SnapshotId, PlacementScope.IncludingSupplemented,
            CancellationToken.None);

        Assert.True(rows.Single(r => r.PlayerId == s.Ours.Id).IsSupplemented);
        Assert.False(rows.Single(r => r.PlayerId == s.Official.Id).IsSupplemented);
    }

    [Fact]
    public async Task ARollUpReplacesItsOwnRowsRatherThanDoublingThem()
    {
        var s = await SeedBothReadings();
        var snapshots = Snapshots();
        var ct = CancellationToken.None;

        await snapshots.DeleteSupplementedPlacements(s.SnapshotId, ct);
        await snapshots.WritePlacements(s.SnapshotId,
            new[] { new PlacementRow(s.Board.Id, s.Ours.Id, 2, 975_000, true) }, ct);

        var rows = await snapshots.GetPlacements(s.SnapshotId, PlacementScope.IncludingSupplemented, ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal(975_000, rows.Single(r => r.IsSupplemented).Score);
        // The official row is untouched by a roll-up, however many times it runs.
        Assert.Equal(990_000, rows.Single(r => !r.IsSupplemented).Score);
    }

    [Fact]
    public async Task DeletingAnAccountTakesItsSupplementedRowsAndLeavesTheOfficialOnes()
    {
        // Nothing in the four-way purge ratchet can reach this: placements key on PlayerId, so
        // no manifest names them. This test is the guard.
        var userId = Guid.NewGuid();
        var s = await SeedBothReadings(userId);
        var ct = CancellationToken.None;

        await Purge().UnlinkUser(userId, ct);

        var rows = await Snapshots().GetPlacements(s.SnapshotId, PlacementScope.IncludingSupplemented, ct);
        Assert.Equal(s.Official.Id, Assert.Single(rows).PlayerId);
    }

    [Fact]
    public async Task DeletingAnAccountLeavesAStrangersSupplementedRowsAlone()
    {
        var s = await SeedBothReadings(Guid.NewGuid());
        var ct = CancellationToken.None;

        await Purge().UnlinkUser(Guid.NewGuid(), ct);

        Assert.Equal(2, (await Snapshots().GetPlacements(s.SnapshotId, PlacementScope.IncludingSupplemented, ct))
            .Count);
    }

    [Fact]
    public async Task HighlightsAreStoredAndReadPerReading()
    {
        var s = await SeedBothReadings();
        var records = Records();
        var ct = CancellationToken.None;

        await records.WriteHighlights(s.SnapshotId, MixEnum.Phoenix2, new[]
        {
            new HighlightRow(HighlightKinds.WeeklyPulse, 1, null, null, null, null, null, 0, null, 1, 2, 3)
        }, false, ct);
        await records.WriteHighlights(s.SnapshotId, MixEnum.Phoenix2, new[]
        {
            new HighlightRow(HighlightKinds.WeeklyPulse, 1, null, null, null, null, null, 0, null, 9, 8, 7)
        }, true, ct);

        Assert.Equal(1, (await records.GetHighlights(s.SnapshotId, false, ct)).Single().Score);
        Assert.Equal(9, (await records.GetHighlights(s.SnapshotId, true, ct)).Single().Score);

        await records.DeleteSupplementedHighlights(s.SnapshotId, ct);
        Assert.Single(await records.GetHighlights(s.SnapshotId, false, ct));
        Assert.Empty(await records.GetHighlights(s.SnapshotId, true, ct));
    }

    [Fact]
    public async Task AnySupplementedAnswersTheBaselineQuestion()
    {
        var ct = CancellationToken.None;
        var snapshots = Snapshots();
        Assert.False(await snapshots.AnySupplemented(MixEnum.Phoenix2, ct));

        var s = await SeedBothReadings();
        Assert.True(await snapshots.AnySupplemented(MixEnum.Phoenix2, ct));
        Assert.False(await snapshots.AnySupplemented(MixEnum.Phoenix, ct));

        var (players, rows) = await snapshots.CountSupplemented(s.SnapshotId, ct);
        Assert.Equal(1, players);
        Assert.Equal(1, rows);
    }
}
