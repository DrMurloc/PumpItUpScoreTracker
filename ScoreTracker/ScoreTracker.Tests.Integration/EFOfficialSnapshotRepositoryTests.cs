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

        Assert.Empty(await snapshots.GetPlacements(staleId, PlacementScope.OfficialOnly, CancellationToken.None));
        Assert.Single(await snapshots.GetPlacements(kept.snapshotId, PlacementScope.OfficialOnly, CancellationToken.None),
            p => p.Place == 1);
        Assert.Equal(kept.snapshotId, (await snapshots.GetLatestSealed(MixEnum.Phoenix2, CancellationToken.None))!.Id);
    }

    [Fact]
    public async Task OnlyAFreshHeartbeatCountsAsALiveRun()
    {
        var snapshots = Snapshots();
        var snapshotId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);

        // Created just now → live for any cutoff at or before creation.
        Assert.True(await snapshots.HasLiveRun(MixEnum.Phoenix2, Week1, CancellationToken.None));
        // A run whose last heartbeat predates the cutoff is dead — it must not hold the lock.
        Assert.False(await snapshots.HasLiveRun(MixEnum.Phoenix2, Week1.AddMinutes(15), CancellationToken.None));

        // A checkpoint revives it past the cutoff.
        await snapshots.UpdateProgress(snapshotId, "ChartBoards", 600, 10, 0, Week1.AddMinutes(20),
            CancellationToken.None);
        Assert.True(await snapshots.HasLiveRun(MixEnum.Phoenix2, Week1.AddMinutes(15), CancellationToken.None));

        // Sealed runs never count, however fresh the heartbeat.
        await snapshots.Seal(snapshotId, Week1.AddMinutes(41), CancellationToken.None);
        Assert.False(await snapshots.HasLiveRun(MixEnum.Phoenix2, Week1.AddMinutes(15), CancellationToken.None));
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
        }, false, CancellationToken.None);

        var highlights = await records.GetHighlights(snapshotId, false, CancellationToken.None);

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

        var week2 = await snapshots.GetPlacements(week2Id, PlacementScope.OfficialOnly, CancellationToken.None);
        Assert.Equal(2, week2.Count);
        Assert.All(week2, p => Assert.Equal(survivor.Id, p.PlayerId));
        Assert.Equal(1, week2.Single(p => p.LeaderboardId == board.Id).Place);
        var week1 = await snapshots.GetPlacements(week1Id, PlacementScope.OfficialOnly, CancellationToken.None);
        Assert.All(week1.Where(p => p.Place == 1), p => Assert.Equal(survivor.Id, p.PlayerId));
        Assert.DoesNotContain(await snapshots.GetPlayers(MixEnum.Phoenix2, CancellationToken.None),
            p => p.Id == oldPlayer.Id);
    }

    [Fact]
    public async Task MergePlayersKeepsThePublishedRowWhenASupplementedOneCollides()
    {
        var snapshots = Snapshots();
        var (_, board, oldPlayer, survivor) = await SeedSealedSnapshot(Week1);
        var week2Id = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2, CancellationToken.None);
        // The old tag genuinely charted this week. The new tag holds only a supplemented
        // stand-in on the same board, because the roll-up found no row under its player id.
        await snapshots.WritePlacements(week2Id, new[]
        {
            new PlacementRow(board.Id, oldPlayer.Id, 12, 980000),
            new PlacementRow(board.Id, survivor.Id, 400, 980000, IsSupplemented: true)
        }, CancellationToken.None);
        await snapshots.Seal(week2Id, Week2.AddMinutes(41), CancellationToken.None);

        await Identity().MergePlayers(oldPlayer.Id, survivor.Id, CancellationToken.None);

        // The real placement survives under the survivor. Dropping it would delete the player
        // from a board the crawl saw them on, and the official reading would never know.
        var official = await snapshots.GetPlacements(week2Id, PlacementScope.OfficialOnly,
            CancellationToken.None);
        var kept = Assert.Single(official);
        Assert.Equal(survivor.Id, kept.PlayerId);
        Assert.Equal(12, kept.Place);
        // And the stand-in is gone rather than sitting beside it — one player, one board row.
        Assert.Single(await snapshots.GetPlacements(week2Id, PlacementScope.IncludingSupplemented,
            CancellationToken.None));
    }

    private static RenameProposal Finding(string verdict = VanishVerdicts.Merge, int? newPlayerId = 22,
        int exactNonPg = 46) =>
        new(0, OldPlayerId: 11, newPlayerId, "OLDTAG", newPlayerId == null ? null : "NEWTAG", verdict,
            new RenameEvidence(50, 48, exactNonPg, 2, 0, 0, true), ProposalStatuses.Pending,
            CreatedSnapshotId: 1);

    [Fact]
    public async Task MergingIntoADeletedPlayerMovesNothing()
    {
        // A finding can sit on the desk for weeks while other merges delete its candidate out
        // from under it. There is no foreign key on PlayerId, so re-pointing history at a row
        // that no longer exists would commit silently and render as blank names on the boards.
        var snapshots = Snapshots();
        var (week1Id, board, oldPlayer, doomed) = await SeedSealedSnapshot(Week1);
        var bystander = (await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("carol", (Uri?)null) }, Week1, CancellationToken.None)).Single();
        await Identity().MergePlayers(doomed.Id, bystander.Id, CancellationToken.None);

        var outcome = await Identity().MergePlayers(oldPlayer.Id, doomed.Id, CancellationToken.None);

        Assert.Equal(MergeOutcome.PlayerGone, outcome);
        var rows = await snapshots.GetPlacements(week1Id, PlacementScope.OfficialOnly,
            CancellationToken.None);
        Assert.Contains(rows, p => p.PlayerId == oldPlayer.Id && p.LeaderboardId == board.Id);
        Assert.DoesNotContain(rows, p => p.PlayerId == doomed.Id);
    }

    [Fact]
    public async Task TwoTagsHeldByDifferentAccountsNeverMerge()
    {
        // A link is either proved by logging into the account or inferred from the game tag an
        // import wrote. One human's two mirror rows carry the same account; two accounts is the
        // site's own answer that these are two people, whatever the scores agree on.
        var snapshots = Snapshots();
        var (week1Id, _, oldPlayer, other) = await SeedSealedSnapshot(Week1);
        var identity = Identity();
        await identity.LinkPlayer(MixEnum.Phoenix2, "alice", Guid.NewGuid(), Week1, CancellationToken.None);
        await identity.LinkPlayer(MixEnum.Phoenix2, "bob", Guid.NewGuid(), Week1, CancellationToken.None);

        var outcome = await identity.MergePlayers(oldPlayer.Id, other.Id, CancellationToken.None);

        Assert.Equal(MergeOutcome.DifferentAccounts, outcome);
        Assert.Contains(await snapshots.GetPlacements(week1Id, PlacementScope.OfficialOnly,
            CancellationToken.None), p => p.PlayerId == oldPlayer.Id);
        Assert.NotNull(await snapshots.GetPlayerByUsername(MixEnum.Phoenix2, "alice", CancellationToken.None));
    }

    [Fact]
    public async Task ResolvedHistoryIsCappedButUnresolvedWorkIsNot()
    {
        var identity = Identity();
        var findings = Enumerable.Range(1, 260)
            .Select(i => Finding(newPlayerId: 1000 + i) with { OldPlayerId = i })
            .ToArray();
        await identity.WriteFindings(MixEnum.Phoenix2, findings, CancellationToken.None);
        var written = await identity.GetFindings(MixEnum.Phoenix2, true, CancellationToken.None);
        foreach (var f in written.Take(250))
            await identity.SetProposalStatus(f.Id, ProposalStatuses.AutoAccepted, CancellationToken.None);

        var desk = await identity.GetFindings(MixEnum.Phoenix2, false, CancellationToken.None);

        // Ten still need a decision; the merged history behind them is trimmed rather than
        // rendering a year of rows into a Blazor circuit.
        Assert.Equal(10, desk.Count(f => f.Status == ProposalStatuses.Pending));
        Assert.Equal(200, desk.Count(f => f.Status == ProposalStatuses.AutoAccepted));
    }

    [Fact]
    public async Task WriteFindingsDeduplicatesRedetectedPairs()
    {
        var identity = Identity();
        await identity.WriteFindings(MixEnum.Phoenix2, new[] { Finding() }, CancellationToken.None);
        await identity.WriteFindings(MixEnum.Phoenix2,
            new[] { Finding() with { CreatedSnapshotId = 2 } }, CancellationToken.None);

        var findings = await identity.GetFindings(MixEnum.Phoenix2, true, CancellationToken.None);

        Assert.Single(findings);
        Assert.Equal(1, findings[0].CreatedSnapshotId);
    }

    [Fact]
    public async Task WriteFindingsReturnsWhatItWroteWithIdsAttached()
    {
        // The sweep merges the conclusive findings straight after writing them, and it needs
        // the ids to send them through the accept path rather than a private merge.
        var written = await WriteOneFinding();

        Assert.NotEqual(0, written.Id);
        Assert.Equal(MixEnum.Phoenix2, written.Mix);
        Assert.Equal(VanishVerdicts.Merge, written.Verdict);
    }

    [Fact]
    public async Task EvidenceSurvivesTheRoundTrip()
    {
        var written = await WriteOneFinding();

        var stored = (await Identity().GetProposal(written.Id, CancellationToken.None))!;

        Assert.Equal(new RenameEvidence(50, 48, 46, 2, 0, 0, true), stored.Evidence);
        Assert.Equal(VanishVerdicts.Merge, stored.Verdict);
    }

    [Fact]
    public async Task ATagWithNoCandidateStoresWithoutOne()
    {
        var identity = Identity();
        await identity.WriteFindings(MixEnum.Phoenix2,
            new[] { Finding(VanishVerdicts.DroppedOff, null, 0) }, CancellationToken.None);

        var stored = Assert.Single(await identity.GetFindings(MixEnum.Phoenix2, true, CancellationToken.None));

        Assert.Null(stored.NewPlayerId);
        Assert.Null(stored.NewUsername);
        Assert.Equal(VanishVerdicts.DroppedOff, stored.Verdict);
    }

    [Fact]
    public async Task ResolvedFindingsLeaveTheQueueButStayOnTheDesk()
    {
        var identity = Identity();
        var written = await WriteOneFinding();

        await identity.SetProposalStatus(written.Id, ProposalStatuses.AutoAccepted, CancellationToken.None);

        Assert.Empty(await identity.GetFindings(MixEnum.Phoenix2, true, CancellationToken.None));
        // Still visible in the full population — an unattended merge that nobody can see
        // afterwards is a one-way door with no record of who walked through it.
        var all = Assert.Single(await identity.GetFindings(MixEnum.Phoenix2, false, CancellationToken.None));
        Assert.Equal(ProposalStatuses.AutoAccepted, all.Status);
    }

    private async Task<RenameProposal> WriteOneFinding() =>
        (await Identity().WriteFindings(MixEnum.Phoenix2, new[] { Finding() }, CancellationToken.None)).Single();

    [Fact]
    public async Task PlayerTimelineSpansSealedSnapshotsInOrderAndSkipsUnsealed()
    {
        var snapshots = Snapshots();
        var week1 = await SeedSealedSnapshot(Week1);
        var week2 = await SeedSealedSnapshot(Week2);
        // An unsealed run's rows must never appear in a timeline.
        var unsealedId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week2.AddDays(1),
            CancellationToken.None);
        await snapshots.WritePlacements(unsealedId, new[]
        {
            new PlacementRow(week2.board.Id, week2.alice.Id, 1, 999000)
        }, CancellationToken.None);

        var timeline = await snapshots.GetPlayerTimeline(week2.alice.Id, PlacementScope.OfficialOnly, CancellationToken.None);

        Assert.Equal(2, timeline.Count);
        Assert.Equal(week1.snapshotId, timeline[0].SnapshotId);
        Assert.Equal(week2.snapshotId, timeline[1].SnapshotId);
        Assert.True(timeline[0].CompletedAt < timeline[1].CompletedAt);
        Assert.Equal(LeaderboardTypes.Chart, timeline[0].LeaderboardType);
        Assert.Equal(week2.board.ChartId, timeline[1].ChartId);
    }

    [Fact]
    public async Task MissingChartsUpsertOncePerIdentityAndRefreshLastIdentified()
    {
        var snapshots = Snapshots();
        var sighting = new MissingChartSighting("Mystery Song", "Double", 27);
        await snapshots.UpsertMissingCharts(MixEnum.Phoenix2, new[] { sighting, sighting }, Week1,
            CancellationToken.None);
        await snapshots.UpsertMissingCharts(MixEnum.Phoenix2,
            new[] { sighting, new MissingChartSighting("Other Song", "Single", 24) }, Week2,
            CancellationToken.None);

        var inbox = await snapshots.GetMissingCharts(MixEnum.Phoenix2, CancellationToken.None);

        Assert.Equal(2, inbox.Count);
        var mystery = inbox.Single(m => m.SongName == "Mystery Song");
        Assert.Equal(Week1, mystery.FirstIdentified);
        Assert.Equal(Week2, mystery.LastIdentified);
        Assert.Empty(await snapshots.GetMissingCharts(MixEnum.Phoenix, CancellationToken.None));

        await snapshots.DeleteMissingChart(mystery.Id, CancellationToken.None);
        Assert.Single(await snapshots.GetMissingCharts(MixEnum.Phoenix2, CancellationToken.None));
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

    [Fact]
    public async Task TagSearchMatchesPartOfATagAndCapsWhatItReturns()
    {
        var (snapshotId, _, _, _) = await SeedSealedSnapshot(Week1);

        var matched = await Snapshots()
            .SearchPlayersInSnapshot(snapshotId, "li", 10, CancellationToken.None);
        var capped = await Snapshots()
            .SearchPlayersInSnapshot(snapshotId, "b", 1, CancellationToken.None);

        Assert.Equal(new[] { "alice" }, matched.Select(p => p.Username));
        Assert.Single(capped);
    }

    [Fact]
    public async Task TagSearchRanksAnExactTagThenAPrefixThenAnythingContainingTheTerm()
    {
        var snapshots = Snapshots();
        var snapshotId = await snapshots.CreateRun(MixEnum.Phoenix2, false, Week1, CancellationToken.None);
        var board = await snapshots.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart, "District 1 D26",
            Guid.NewGuid(), "Double", 26, CancellationToken.None);
        var players = await snapshots.EnsurePlayers(MixEnum.Phoenix2,
            new[] { ("abe", (Uri?)null), ("bob", (Uri?)null), ("b", (Uri?)null) }, Week1, CancellationToken.None);
        await snapshots.WritePlacements(snapshotId,
            players.Select((p, i) => new PlacementRow(board.Id, p.Id, i + 1, 990000 - i)).ToArray(),
            CancellationToken.None);
        await snapshots.Seal(snapshotId, Week1.AddMinutes(41), CancellationToken.None);

        var hits = await Snapshots().SearchPlayersInSnapshot(snapshotId, "b", 10, CancellationToken.None);

        Assert.Equal(new[] { "b", "bob", "abe" }, hits.Select(p => p.Username));
    }

    [Fact]
    public async Task TagSearchExcludesATagThatLeftTheBoards()
    {
        // The dimension keeps every tag ever seen; this snapshot is the population a PICKER may
        // offer, because a departed tag is a permanently empty rivalry (rivals.md D21).
        var (snapshotId, _, _, _) = await SeedSealedSnapshot(Week1);
        await Snapshots().EnsurePlayers(MixEnum.Phoenix2, new[] { ("alistair", (Uri?)null) }, Week1,
            CancellationToken.None);

        var matched = await Snapshots()
            .SearchPlayersInSnapshot(snapshotId, "ali", 10, CancellationToken.None);

        Assert.Equal(new[] { "alice" }, matched.Select(p => p.Username));
    }

    [Fact]
    public async Task FilterNamesKeepsOnlyTheTagsThatPlacedInTheSnapshot()
    {
        var (snapshotId, _, _, _) = await SeedSealedSnapshot(Week1);
        await Snapshots().EnsurePlayers(MixEnum.Phoenix2, new[] { ("departed", (Uri?)null) }, Week1,
            CancellationToken.None);

        var current = await Snapshots().FilterNamesInSnapshot(snapshotId,
            new[] { "alice", "departed", "never-existed" }, CancellationToken.None);

        Assert.Equal(new[] { "alice" }, current);
    }

    [Fact]
    public async Task TagSearchAsksTheDatabaseForNothingWhenThereIsNothingToAskFor()
    {
        var (snapshotId, _, _, _) = await SeedSealedSnapshot(Week1);

        Assert.Empty(await Snapshots().SearchPlayersInSnapshot(snapshotId, "  ", 10,
            CancellationToken.None));
        Assert.Empty(await Snapshots().SearchPlayersInSnapshot(snapshotId, "alice", 0,
            CancellationToken.None));
        Assert.Empty(await Snapshots().FilterNamesInSnapshot(snapshotId, Array.Empty<string>(),
            CancellationToken.None));
    }

    [Fact]
    public async Task PlayersByUserIdsReadTheLinkedRowsOfOneMixAndNothingElse()
    {
        var identity = Identity();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await identity.LinkPlayer(MixEnum.Phoenix2, "alice", alice, Week1, CancellationToken.None);
        await identity.LinkPlayer(MixEnum.Phoenix2, "bob", bob, Week1, CancellationToken.None);
        await identity.LinkPlayer(MixEnum.Phoenix, "alice-on-phoenix", alice, Week1, CancellationToken.None);

        var players = await Snapshots().GetPlayersByUserIds(MixEnum.Phoenix2, new[] { alice, bob, stranger },
            CancellationToken.None);

        Assert.Equal(2, players.Count);
        Assert.Equal("alice", players.Single(p => p.UserId == alice).Username);
        Assert.Equal("bob", players.Single(p => p.UserId == bob).Username);
        Assert.Empty(await Snapshots().GetPlayersByUserIds(MixEnum.Phoenix2, Array.Empty<Guid>(),
            CancellationToken.None));
        // The single read answers the same tag as the bulk one for the same account and mix.
        Assert.Equal("alice", (await Snapshots().GetPlayerByUserId(MixEnum.Phoenix2, alice, CancellationToken.None))!.Username);
        Assert.Null(await Snapshots().GetPlayerByUserId(MixEnum.Phoenix2, stranger, CancellationToken.None));
    }
}
