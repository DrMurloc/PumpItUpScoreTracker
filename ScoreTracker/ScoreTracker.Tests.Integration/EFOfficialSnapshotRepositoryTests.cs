using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFOfficialSnapshotRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Week1 = new(2026, 7, 5, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week2 = new(2026, 7, 12, 16, 30, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _fixture;

    public EFOfficialSnapshotRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFOfficialSnapshotRepository Snapshots() => new(_fixture.DbContextFactory);
    private EFOfficialRecordRepository Records() => new(_fixture.DbContextFactory);
    private EFOfficialPlayerIdentityRepository Identity() => new(_fixture.DbContextFactory);

    private async Task<(int snapshotId, BoardDimension board, PlayerDimension alice, PlayerDimension bob)>
        SeedSealedSnapshot(DateTimeOffset at, decimal aliceScore = 995000, decimal bobScore = 990000)
    {
        var snapshots = Snapshots();
        var snapshotId = await snapshots.CreateRun(MixEnum.Phoenix2, false, at, CancellationToken.None);
        var board = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "District 1 D26",
            Guid.NewGuid(), "Double", 26, CancellationToken.None);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("alice", (Uri?)null), ("bob", (Uri?)null) }, at, CancellationToken.None);
        await snapshots.WritePlacements(snapshotId, new[]
        {
            new PlacementRow(board.Id, players[0].Id, 1, aliceScore),
            new PlacementRow(board.Id, players[1].Id, 2, bobScore)
        }, CancellationToken.None);
        await snapshots.Seal(snapshotId, at.AddMinutes(41), CancellationToken.None);
        return (snapshotId, board, players[0], players[1]);
    }

    [Fact]
    public async Task UnsealedRunsAreInvisibleToLatestSealed()
    {
        var snapshots = Snapshots();
        var sealedRun = await SeedSealedSnapshot(Week1);
        await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2, CancellationToken.None);

        var latest = await snapshots.GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(sealedRun.snapshotId, latest!.Id);
    }

    [Fact]
    public async Task LatestSealedTracksTheNewestCompletedRunPerMix()
    {
        var first = await SeedSealedSnapshot(Week1);
        var second = await SeedSealedSnapshot(Week2);

        var latest = await Snapshots().GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None);
        var phoenix = await Snapshots().GetLatestSealed(MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal(second.snapshotId, latest!.Id);
        Assert.NotEqual(first.snapshotId, latest.Id);
        Assert.Null(phoenix);
    }

    [Fact]
    public async Task GetSealedBeforeReturnsTheDiffBaseline()
    {
        var first = await SeedSealedSnapshot(Week1);
        var second = await SeedSealedSnapshot(Week2);

        var baseline = await Snapshots()
            .GetSealedBefore(MixEnum.Phoenix2, second.snapshotId, CancellationToken.None);

        Assert.Equal(first.snapshotId, baseline!.Id);
        Assert.Null(await Snapshots().GetSealedBefore(MixEnum.Phoenix2, first.snapshotId, CancellationToken.None));
    }

    [Fact]
    public async Task PurgeUnsealedRemovesTheRunAndItsRowsButSparesSealedOnes()
    {
        var snapshots = Snapshots();
        var kept = await SeedSealedSnapshot(Week1);
        var staleId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1.AddDays(-10),
            CancellationToken.None);
        await snapshots.WritePlacements(staleId, new[]
        {
            new PlacementRow(kept.board.Id, kept.alice.Id, 1, 991000)
        }, CancellationToken.None);

        await snapshots.PurgeUnsealed(MixEnum.Phoenix2, Week1.AddDays(-7), CancellationToken.None);

        Assert.Empty(await snapshots.GetPlacements(staleId, CancellationToken.None));
        Assert.Single(await snapshots.GetPlacements(kept.snapshotId, CancellationToken.None),
            p => p.Place == 1);
        Assert.Equal(kept.snapshotId, (await snapshots.GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task EnsureBoardIsIdempotentAndRefreshesChartAssociation()
    {
        var snapshots = Snapshots();
        var chartId = Guid.NewGuid();
        var first = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Altale D24", null,
            "Double", 24, CancellationToken.None);
        var second = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Altale D24", chartId,
            "Double", 24, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(chartId, second.ChartId);
        Assert.Single(await snapshots.GetBoards(MixEnum.Phoenix2, CancellationToken.None));
    }

    [Fact]
    public async Task EnsurePlayersKeepsStoredAvatarWhenIncomingIsNull()
    {
        var snapshots = Snapshots();
        var avatar = new Uri("https://example.invalid/alice.png");
        await snapshots.EnsurePlayers(MixEnum.Phoenix2, new[] { ("alice", (Uri?)avatar) }, Week1,
            CancellationToken.None);

        var second = await snapshots.EnsurePlayers(MixEnum.Phoenix2, new[] { ("alice", (Uri?)null) }, Week2,
            CancellationToken.None);

        Assert.Equal(avatar, second.Single().Avatar);
    }

    [Fact]
    public async Task RecordBooksRoundTripAndScopeByMix()
    {
        var records = Records();
        var board = await Snapshots().EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Kugutsu D26",
            Guid.NewGuid(), "Double", 26, CancellationToken.None);
        await records.UpsertBoardRecords(new[] { new BoardRecordRow(board.Id, 998000, 3) },
            CancellationToken.None);
        await records.UpsertBoardRecords(new[] { new BoardRecordRow(board.Id, 1000000, 4) },
            CancellationToken.None);
        await records.UpsertFolderRecords(MixEnum.Phoenix2, new[] { new FolderRecordRow("Double", 26, 1000000, 4) },
            CancellationToken.None);

        var boardRecords = await records.GetBoardRecords(MixEnum.Phoenix2, CancellationToken.None);
        var folderRecords = await records.GetFolderRecords(MixEnum.Phoenix2, CancellationToken.None);

        Assert.Equal(1000000, boardRecords.Single().HighScore);
        Assert.Equal(4, boardRecords.Single().AchievedSnapshotId);
        Assert.Equal(1000000, folderRecords.Single().HighScore);
        Assert.Empty(await records.GetBoardRecords(MixEnum.Phoenix, CancellationToken.None));
    }

    [Fact]
    public async Task HighlightsRoundTripInKindThenSortOrder()
    {
        var (snapshotId, board, alice, bob) = await SeedSealedSnapshot(Week1);
        var records = Records();
        await records.WriteHighlights(snapshotId, MixEnum.Phoenix2, new[]
        {
            new HighlightRow(HighlightKinds.NewNumberOne, 1, alice.Id, bob.Id, board.Id, board.ChartId,
                "Double", 26, null, 995000, null, null),
            new HighlightRow(HighlightKinds.PumbilityMover, 1, bob.Id, null, null, null, null, null, null,
                17418.45m, 26, 18)
        }, CancellationToken.None);

        var highlights = await records.GetHighlights(snapshotId, CancellationToken.None);

        Assert.Equal(2, highlights.Count);
        var mover = highlights.Single(h => h.Kind == HighlightKinds.PumbilityMover);
        Assert.Equal(17418.45m, mover.Score);
        var newOne = highlights.Single(h => h.Kind == HighlightKinds.NewNumberOne);
        Assert.Equal(bob.Id, newOne.DethronedPlayerId);
    }

    [Fact]
    public async Task LinkPlayerOverwritesThePreviousLink()
    {
        var identity = Identity();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        await identity.LinkPlayer(MixEnum.Phoenix2, "alice", firstUser, Week1, CancellationToken.None);

        await identity.LinkPlayer(MixEnum.Phoenix2, "alice", secondUser, Week2, CancellationToken.None);

        var players = await Snapshots().GetPlayers(MixEnum.Phoenix2, CancellationToken.None);
        Assert.Equal(secondUser, players.Single(p => p.Username == "alice").UserId);
    }

    [Fact]
    public async Task MergePlayersRepointsHistoryDropsCollisionsAndDeletesTheOldRow()
    {
        var snapshots = Snapshots();
        var (week1Id, board, oldPlayer, survivor) = await SeedSealedSnapshot(Week1);
        // Transition week: both tags appear — the old tag on the shared board (collision)
        // and on a second board only it played (clean re-point).
        var secondBoard = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "Altale D24",
            Guid.NewGuid(), "Double", 24, CancellationToken.None);
        var week2Id = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2, CancellationToken.None);
        await snapshots.WritePlacements(week2Id, new[]
        {
            new PlacementRow(board.Id, oldPlayer.Id, 2, 995000),
            new PlacementRow(board.Id, survivor.Id, 1, 996000),
            new PlacementRow(secondBoard.Id, oldPlayer.Id, 5, 970000)
        }, CancellationToken.None);
        await snapshots.Seal(week2Id, Week2.AddMinutes(41), CancellationToken.None);

        await Identity().MergePlayers(oldPlayer.Id, survivor.Id, CancellationToken.None);

        var week2 = await snapshots.GetPlacements(week2Id, CancellationToken.None);
        Assert.Equal(2, week2.Count);
        Assert.All(week2, p => Assert.Equal(survivor.Id, p.PlayerId));
        Assert.Equal(1, week2.Single(p => p.LeaderboardId == board.Id).Place);
        var week1 = await snapshots.GetPlacements(week1Id, CancellationToken.None);
        Assert.All(week1.Where(p => p.Place == 1), p => Assert.Equal(survivor.Id, p.PlayerId));
        Assert.DoesNotContain(await snapshots.GetPlayers(MixEnum.Phoenix2, CancellationToken.None),
            p => p.Id == oldPlayer.Id);
    }

    [Fact]
    public async Task WriteProposalsDeduplicatesRedetectedPairs()
    {
        var identity = Identity();
        var proposal = new RenameProposal(0, OldPlayerId: 11, NewPlayerId: 22, "OLDTAG", "NEWTAG",
            true, 46, ProposalStatuses.Pending, CreatedSnapshotId: 1);
        await identity.WriteProposals(MixEnum.Phoenix2, new[] { proposal }, CancellationToken.None);
        await identity.WriteProposals(MixEnum.Phoenix2, new[] { proposal with { CreatedSnapshotId = 2 } },
            CancellationToken.None);

        var pending = await identity.GetProposals(MixEnum.Phoenix2, ProposalStatuses.Pending,
            CancellationToken.None);

        Assert.Single(pending);
        Assert.Equal(1, pending[0].CreatedSnapshotId);
    }

    [Fact]
    public async Task ProposalStatusTransitionsPersist()
    {
        var identity = Identity();
        await identity.WriteProposals(MixEnum.Phoenix2, new[]
        {
            new RenameProposal(0, 11, 22, "OLDTAG", "NEWTAG", true, 46, ProposalStatuses.Pending, 1)
        }, CancellationToken.None);
        var pending = (await identity.GetProposals(MixEnum.Phoenix2, ProposalStatuses.Pending,
            CancellationToken.None)).Single();

        await identity.SetProposalStatus(pending.Id, ProposalStatuses.Accepted, CancellationToken.None);

        Assert.Empty(await identity.GetProposals(MixEnum.Phoenix2, ProposalStatuses.Pending,
            CancellationToken.None));
        Assert.Equal(ProposalStatuses.Accepted,
            (await identity.GetProposal(pending.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task PopularityRoundTripsPerSnapshot()
    {
        var snapshots = Snapshots();
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var snapshotId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        await snapshots.WritePopularity(snapshotId, new[] { (chartA, 1), (chartB, 2) }, CancellationToken.None);

        var popularity = await snapshots.GetPopularity(snapshotId, CancellationToken.None);

        Assert.Equal(1, popularity.Single(p => p.ChartId == chartA).Place);
        Assert.Equal(2, popularity.Single(p => p.ChartId == chartB).Place);
    }
}
