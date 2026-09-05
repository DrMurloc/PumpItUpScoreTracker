using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class LeaderboardHubSagaTests
{
    private static readonly DateTimeOffset Week2 = new(2026, 7, 12, 17, 11, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week1 = Week2.AddDays(-7);
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    private static SnapshotRun Run(int id, DateTimeOffset at, bool baseline = false)
    {
        return new SnapshotRun(id, at.AddMinutes(-40), at, baseline, "Sealed", 600, 600, 0, null);
    }

    private sealed record Fixture(Mock<IOfficialSnapshotRepository> Snapshots,
        Mock<IOfficialRecordRepository> Records, LeaderboardHubSaga Saga);

    private static Fixture Arrange(SnapshotRun? latest, SnapshotRun? previous = null)
    {
        var snapshots = new Mock<IOfficialSnapshotRepository>();
        snapshots.Setup(s => s.GetLatestSealed(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);
        snapshots.Setup(s => s.GetSealedBefore(It.IsAny<MixEnum>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        snapshots.Setup(s => s.GetPlacementDetails(It.IsAny<int>(), It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlacementDetail>());
        snapshots.Setup(s => s.GetPlayersByIds(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.Select(id => new PlayerDimension(id, $"PLAYER{id}",
                    new Uri($"https://example.invalid/{id}.png"), null)).ToArray());
        var records = new Mock<IOfficialRecordRepository>();
        records.Setup(r => r.GetHighlights(It.IsAny<int>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HighlightRow>());
        var saga = new LeaderboardHubSaga(snapshots.Object, records.Object,
            new MemoryCache(new MemoryCacheOptions()));
        return new Fixture(snapshots, records, saga);
    }

    private static PlacementDetail Chart(int playerId, Guid chartId, int place, decimal score, int level = 24,
        string type = "Single", int boardId = 0)
    {
        return new PlacementDetail(playerId, boardId == 0 ? chartId.GetHashCode() & 0xFFFF : boardId,
            LeaderboardTypes.Chart, $"Board {chartId}", chartId, type, level, place, score);
    }

    private static PlacementDetail Pumbility(int playerId, int place, decimal value, string board = "PUMBILITY")
    {
        return new PlacementDetail(playerId, 900 + board.Length, LeaderboardTypes.Rating, board, null, null,
            null, place, value);
    }

    // Co-op boards carry no difficulty level — the dimension's Level is genuinely null.
    private static PlacementDetail CoOpChart(int playerId, Guid chartId, int place, decimal score)
    {
        return new PlacementDetail(playerId, chartId.GetHashCode() & 0xFFFF, LeaderboardTypes.Chart,
            $"Board {chartId}", chartId, "CoOp", null, place, score);
    }

    [Fact]
    public async Task ChartBoardServesTheLatestSealedBoardInPlaceOrder()
    {
        var fixture = Arrange(Run(4, Week2));
        fixture.Snapshots.Setup(s => s.GetBoards(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BoardDimension(516, LeaderboardTypes.Chart, "1948 D29", ChartA, "Double", 29),
                new BoardDimension(9, LeaderboardTypes.Rating, "PUMBILITY", null, null, null)
            });
        fixture.Snapshots.Setup(s => s.GetBoardPlacements(4, 516, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlacementRow(516, 7, 2, 951210),
                new PlacementRow(516, 3, 1, 962777)
            });

        var board = await fixture.Saga.Handle(new GetOfficialChartBoardQuery(MixEnum.Phoenix2, ChartA),
            CancellationToken.None);

        Assert.NotNull(board);
        Assert.Equal(new[] { 1, 2 }, board!.Entries.Select(e => e.Place).ToArray());
        Assert.Equal("PLAYER3", board.Entries[0].Player.Username);
        Assert.Equal(962777, board.Entries[0].Score);
    }

    [Fact]
    public async Task ChartBoardIsNullWhenTheChartHasNoBoard()
    {
        var fixture = Arrange(Run(4, Week2));
        fixture.Snapshots.Setup(s => s.GetBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardDimension>());

        Assert.Null(await fixture.Saga.Handle(new GetOfficialChartBoardQuery(MixEnum.Phoenix2, ChartA),
            CancellationToken.None));
    }

    [Fact]
    public async Task LinkedTagResolvesThroughThePlayerDimension()
    {
        var fixture = Arrange(Run(4, Week2));
        var userId = Guid.NewGuid();
        fixture.Snapshots.Setup(s => s.GetPlayerByUserId(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerDimension(3, "FEFEMZ#1489", null, userId));

        Assert.Equal("FEFEMZ#1489", await fixture.Saga.Handle(
            new GetLinkedOfficialPlayerTagQuery(MixEnum.Phoenix2, userId), CancellationToken.None));
        Assert.Null(await fixture.Saga.Handle(
            new GetLinkedOfficialPlayerTagQuery(MixEnum.Phoenix2, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task HighlightsAreNullBeforeTheFirstSeal()
    {
        var f = Arrange(latest: null);

        Assert.Null(await f.Saga.Handle(new GetWeeklyHighlightsQuery(MixEnum.Phoenix2),
            CancellationToken.None));
    }

    [Fact]
    public async Task HighlightsGroupByKindAndResolvePlayers()
    {
        var f = Arrange(Run(2, Week2), Run(1, Week1));
        f.Records.Setup(r => r.GetHighlights(2, false, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new HighlightRow(HighlightKinds.PumbilityMover, 1, 11, null, 900, null, null, null, null,
                18204.51m, 31, 17),
            new HighlightRow(HighlightKinds.FolderGradeFirst, 1, 12, null, 10, ChartA, "Double", 26, "PG",
                1_000_000, 998000, null),
            new HighlightRow(HighlightKinds.ChartGradeFirst, 1, 13, null, 11, ChartB, "Single", 24, "SSS",
                991000, 989000, null),
            new HighlightRow(HighlightKinds.NewNumberOne, 1, 14, 15, 12, ChartB, "Single", 25, null,
                997342, 995000, null),
            new HighlightRow(HighlightKinds.BoardsClimbed, 1, 16, null, null, null, null, null, null, null,
                388, 21)
        });

        var result = await f.Saga.Handle(new GetWeeklyHighlightsQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Week2, result!.SnapshotAt);
        Assert.Equal(Week1, result.PreviousSnapshotAt);
        var mover = Assert.Single(result.Movers);
        Assert.Equal("PLAYER11", mover.Player.Username);
        Assert.Equal(31, mover.PreviousRank);
        Assert.Equal(17, mover.NewRank);
        Assert.Equal(2, result.WorldFirsts.Count);
        Assert.True(result.WorldFirsts[0].IsFolderFirst);
        Assert.Equal("PG", result.WorldFirsts[0].GradeBand);
        var numberOne = Assert.Single(result.NewNumberOnes);
        Assert.Equal("PLAYER15", numberOne.Dethroned!.Username);
        var climbed = Assert.Single(result.BoardsClimbed);
        Assert.Equal(21, climbed.BoardsClimbed);
        Assert.Equal(388, climbed.NetPlacesGained);
    }

    [Fact]
    public async Task Phoenix2RankingsFollowTheOfficialBoardWithDeltas()
    {
        var f = Arrange(Run(2, Week2), Run(1, Week1));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Pumbility(11, 1, 19412.88m),
            Pumbility(12, 2, 19205.13m),
            Chart(11, ChartA, 1, 997000, level: 26, type: "Double", boardId: 500),
            Chart(11, ChartB, 2, 993000, level: 24, boardId: 501),
            Chart(12, ChartB, 1, 995000, level: 24, boardId: 501)
        });
        f.Snapshots.Setup(s => s.GetPlacementDetails(1, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Pumbility(11, 2, 19100.00m),
            Pumbility(12, 1, 19180.00m)
        });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.True(result.RatingIsOfficial);
        Assert.Equal(2, result.Rankings.Count);
        var top = result.Rankings[0];
        Assert.Equal(1, top.Rank);
        Assert.Equal(2, top.PreviousRank);
        Assert.Equal("PLAYER11", top.Player.Username);
        Assert.Equal(19412.88m, top.Rating);
        Assert.Equal(2, top.BoardsInTop);
    }

    /// <summary>
    ///     A mix whose PUMBILITY board the mirror has not got yet shows nothing, and waits.
    ///     This used to compute a stand-in from chart placements, which ranked thousands of
    ///     players on a board the site publishes 1,000 of — and invented Singles and Doubles
    ///     boards for a mix that has neither. The rankings are the official ranking or they
    ///     are not the rankings.
    /// </summary>
    [Fact]
    public async Task AMixWithNoMirroredPumbilityBoardRanksNobody()
    {
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Chart(11, ChartA, 1, 995000, level: 24, boardId: 500),
            Chart(12, ChartA, 2, 970000, level: 24, boardId: 500)
        });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Empty(result.Rankings);
        Assert.True(result.RatingIsOfficial);
    }

    [Fact]
    public async Task CoOpRankingsComputeFromCoOpBoardsWithInferredPlates()
    {
        var f = Arrange(Run(2, Week2));
        var coopA = Guid.NewGuid();
        var coopB = Guid.NewGuid();
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            CoOpChart(11, coopA, 1, 1_000_000),
            CoOpChart(11, coopB, 2, 995_000),
            CoOpChart(12, coopA, 2, 994_999),
            // A standard board must not leak into the co-op totals or board counts.
            Chart(12, ChartA, 1, 999_000, level: 26, boardId: 500)
        });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix2, "CoOp"),
            CancellationToken.None);

        Assert.False(result.RatingIsOfficial);
        Assert.Equal(2, result.Rankings.Count);
        var top = result.Rankings[0];
        Assert.Equal("PLAYER11", top.Player.Username);
        // Phoenix 2's CO-OP Rating: 80 × (1.50 + .020) at the perfect + 80 × (1.50 + .016) at 995k.
        Assert.Equal(121.60m + 121.28m, top.Rating, 2);
        Assert.Equal(2, top.BoardsInTop);
        var second = result.Rankings[1];
        Assert.Equal(119.84m, second.Rating, 2); // SSS with an inferred SG — the standard chart stays out.
        Assert.Equal(1, second.BoardsInTop);
    }

    [Fact]
    public async Task CoOpRankingsSumEveryMirroredChartWithNoTopFiftyCut()
    {
        // The CO-OP Rating is an all-charts sum — the [CO-OP] LV.10 title sits at 10,000, which
        // fifty perfects (6,080) could never reach. Sixty perfects on the board: 60 × 121.60.
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 60).Select(_ => CoOpChart(11, Guid.NewGuid(), 1, 1_000_000)).ToArray());

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix2, "CoOp"),
            CancellationToken.None);

        var top = Assert.Single(result.Rankings);
        Assert.Equal(60 * 121.60m, top.Rating, 2);
        Assert.Equal(60, top.BoardsInTop);
    }

    [Fact]
    public async Task StandardRankingsNeverCountCoOpBoards()
    {
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            // The board itself is what ranks the standard views now, so it has to be here.
            Pumbility(11, 1, 17_900m),
            Chart(11, ChartA, 1, 995_000, level: 24, boardId: 500),
            // Player 12 lives only on co-op boards — invisible to the standard views.
            CoOpChart(12, Guid.NewGuid(), 1, 1_000_000)
        });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix),
            CancellationToken.None);

        var only = Assert.Single(result.Rankings);
        Assert.Equal("PLAYER11", only.Player.Username);
    }

    [Fact]
    public async Task ProfileBuildsTilesHistoryAndPlacementDeltas()
    {
        var f = Arrange(Run(2, Week2), Run(1, Week1));
        var player = new PlayerDimension(11, "NIMBUS9", null, null);
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "NIMBUS9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Chart(11, ChartA, 1, 999120, level: 24, type: "Double", boardId: 500)
        });
        f.Snapshots.Setup(s => s.GetPlayerTimeline(11, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new PlayerTimelineRow(1, Week1, LeaderboardTypes.Rating, "PUMBILITY", null, 9, 17755.00m),
            new PlayerTimelineRow(1, Week1, LeaderboardTypes.Chart, "Altale D24", ChartA, 3, 998000),
            new PlayerTimelineRow(2, Week2, LeaderboardTypes.Rating, "PUMBILITY", null, 8, 17903.40m),
            new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Altale D24", ChartA, 1, 999120)
        });

        var profile = await f.Saga.Handle(new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "NIMBUS9"),
            CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(17903.40m, profile!.Pumbility);
        Assert.Equal(8, profile.PumbilityRank);
        Assert.Equal(1, profile.PumbilityRankDelta);
        Assert.Equal(1, profile.BoardsInTop);
        Assert.Equal(1, profile.NumberOnes);
        Assert.Equal(2, profile.History.Count);
        Assert.Equal(17755.00m, profile.History[0].Pumbility);
        var placement = Assert.Single(profile.Placements);
        Assert.Equal(2, placement.PlaceDelta); // 3 → 1
        Assert.True(placement.ComputedRating > 0);
    }

    [Fact]
    public async Task AProfileListsMirroredBoardRowsOnlyEvenForALinkedAccount()
    {
        // The hub reads the mirror, never the site ledger: a linked player with one
        // sparse board gets exactly that row, and every listed chart carries a place.
        var f = Arrange(Run(2, Week2));
        var boardChart = new ChartBuilder().WithLevel(24).WithType(ChartType.Single).Build();
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "NIMBUS9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerDimension(11, "NIMBUS9", null, Guid.NewGuid()));
        f.Snapshots.Setup(s => s.GetPlayerTimeline(11, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Board", boardChart.Id, 3, 991000)
        });

        var profile = await f.Saga.Handle(new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "NIMBUS9"),
            CancellationToken.None);

        Assert.NotNull(profile);
        var placement = Assert.Single(profile!.Placements);
        Assert.Equal(boardChart.Id, placement.ChartId);
        Assert.Equal(3, placement.Place);
        Assert.Equal(1, profile.BoardsInTop);
    }

    [Fact]
    public async Task PopularityCarriesPreviousPlaceAndTrend()
    {
        var f = Arrange(Run(3, Week2));
        f.Snapshots.Setup(s => s.GetPopularityHistory(MixEnum.Phoenix2, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (3, ChartA, 1), (3, ChartB, 2),
                (2, ChartA, 3), (2, ChartB, 1),
                (1, ChartA, 4)
            });

        var result = await f.Saga.Handle(new GetOfficialPopularityQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(ChartA, result[0].ChartId);
        Assert.Equal(3, result[0].PreviousPlace);
        Assert.Equal(new[] { 4, 3, 1 }, result[0].RecentPlaces);
        Assert.Equal(new[] { 1, 2 }, result[1].RecentPlaces);
    }

    [Fact]
    public async Task WhatItTakesBuildsTheLadderFromAFullBoard()
    {
        var f = Arrange(Run(2, Week2), Run(1, Week1));
        var fullBoard = Enumerable.Range(1, 1000)
            .Select(rank => Pumbility(rank, rank, 21000m - rank * 8))
            .ToArray();
        var previousBoard = Enumerable.Range(1, 1000)
            .Select(rank => Pumbility(rank, rank, 20900m - rank * 8))
            .ToArray();
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullBoard);
        f.Snapshots.Setup(s => s.GetPlacementDetails(1, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousBoard);
        f.Snapshots.Setup(s => s.GetBoardFloorHistory(MixEnum.Phoenix2, "PUMBILITY",
                It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (1, Week1, 12900m, 1000), (2, Week2, 13000m, 1000) });

        var result = await f.Saga.Handle(new GetWhatItTakesQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.True(result.BoardFull);
        Assert.Equal(1000, result.BoardCount);
        Assert.NotNull(result.Entry);
        Assert.Equal(13000m, result.Entry!.Value);
        Assert.Equal(100m, result.Entry.WeekDelta);
        Assert.NotNull(result.Entry.LevelForAAA);
        // Higher grades buy in at lower levels.
        Assert.True(result.Entry.LevelForSSS <= result.Entry.LevelForAAA);
        Assert.Equal(28, result.Tiers.Count);
        Assert.Equal(2, result.History.Count);
        // Only the boards the snapshot actually holds: this one mirrored the All board.
        Assert.Equal(new[] { "All" }, result.Boards.Select(b => b.Type).ToArray());
        Assert.Equal(13000m, result.Boards.Single(b => b.Type == "All").EntryValue);
    }

    [Fact]
    public async Task WhatItTakesOnlyComparesBoardsTheMixPublishes()
    {
        // Phoenix serves one PUMBILITY list with no per-type split; Phoenix 2 splits it in
        // three. The comparison strip names what was mirrored, never an invented board.
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 1000)
                .SelectMany(rank => new[]
                {
                    Pumbility(rank, rank, 21000m - rank * 8),
                    Pumbility(rank, rank, 19000m - rank * 8, "PUMBILITY Singles")
                })
                .ToArray());
        f.Snapshots.Setup(s => s.GetBoardFloorHistory(MixEnum.Phoenix2, "PUMBILITY",
                It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int, DateTimeOffset, decimal, int)>());

        var result = await f.Saga.Handle(new GetWhatItTakesQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(new[] { "All", "Singles" }, result.Boards.Select(b => b.Type).ToArray());
        Assert.Equal(11000m, result.Boards.Single(b => b.Type == "Singles").EntryValue);
    }

    [Fact]
    public async Task WhatItTakesOnAnUnfullBoardHasNoEntryButStillLaddersWhatExists()
    {
        var f = Arrange(Run(2, Week2));
        var partialBoard = Enumerable.Range(1, 250)
            .Select(rank => Pumbility(rank, rank, 18000m - rank * 10))
            .ToArray();
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(partialBoard);
        f.Snapshots.Setup(s => s.GetBoardFloorHistory(MixEnum.Phoenix2, "PUMBILITY",
                It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (2, Week2, 15500m, 250) });

        var result = await f.Saga.Handle(new GetWhatItTakesQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.False(result.BoardFull);
        Assert.Equal(250, result.BoardCount);
        Assert.Null(result.Entry);
        // Ranks the board reaches (200, 100, the tens, the seats) ladder; deeper rungs don't.
        Assert.Contains(result.Tiers, t => t.Rank == 200);
        Assert.DoesNotContain(result.Tiers, t => t.Rank == 300);
        // Never-full boards contribute no inflation history.
        Assert.Empty(result.History);
    }

    [Fact]
    public async Task ImportRunsProjectTheRunState()
    {
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetRecentRuns(MixEnum.Phoenix2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Run(2, Week2),
                new SnapshotRun(1, Week1.AddMinutes(-18), null, false, "ChartBoards", 609, 214, 2,
                    "HTTP 503 from piugame.com")
            });

        var runs = await f.Saga.Handle(new GetImportRunsQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(2, runs.Count);
        Assert.Null(runs[1].CompletedAt);
        Assert.Equal("ChartBoards", runs[1].Stage);
        Assert.Contains("503", runs[1].Error);
    }

    /// <summary>
    ///     The supplemented ranking interleaves two rulers -- piugame's number for board players,
    ///     ours for everyone else -- and the row is the only place a reader can tell them apart.
    ///     The mark used to be dropped twice on the way to the row: once when the snapshot stats
    ///     rebuilt the rating board, once when the dimension lookup rebuilt the player. Every
    ///     merged row read as official, and the Rankings rail had nothing to draw.
    /// </summary>
    [Fact]
    public async Task SupplementedRankingRowsCarryTheMarkAndOfficialRowsDoNot()
    {
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, PlacementScope.IncludingSupplemented,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Pumbility(11, 1, 19412.88m),
                Pumbility(12, 2, 19205.13m) with { IsSupplemented = true },
                Chart(11, ChartA, 1, 997000, level: 24, boardId: 500),
                Chart(12, ChartA, 301, 951000, level: 24, boardId: 500) with { IsSupplemented = true }
            });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix2, Supplemented: true),
            CancellationToken.None);

        Assert.Equal(2, result.Rankings.Count);
        Assert.False(result.Rankings[0].Player.IsSupplemented);
        Assert.Equal("PLAYER11", result.Rankings[0].Player.Username);
        Assert.True(result.Rankings[1].Player.IsSupplemented);
        Assert.Equal("PLAYER12", result.Rankings[1].Player.Username);
    }

    /// <summary>
    ///     An off-board player's profile PUMBILITY is the site's computed number. The profile says
    ///     so on the value itself, separately from the player-level mark, because a player can be
    ///     on a chart board officially and still be absent from piugame's PUMBILITY ranking.
    /// </summary>
    [Fact]
    public async Task ProfileSaysWhenItsPumbilityCameFromTheSupplementedRow()
    {
        var f = Arrange(Run(2, Week2));
        var player = new PlayerDimension(12, "NEWCOMER", null, null);
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "NEWCOMER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        f.Snapshots.Setup(s => s.GetPlayerTimeline(12, PlacementScope.IncludingSupplemented,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlayerTimelineRow(2, Week2, LeaderboardTypes.Rating, "PUMBILITY", null, 1432, 14320.50m,
                    IsSupplemented: true),
                new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Altale D24", ChartA, 1, 999120)
            });

        var supplemented = await f.Saga.Handle(
            new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "NEWCOMER", Supplemented: true),
            CancellationToken.None);

        Assert.NotNull(supplemented);
        Assert.Equal(14320.50m, supplemented!.Pumbility);
        Assert.True(supplemented.PumbilityIsSupplemented);
        // The chart row is official, so the player-level mark stays off: only the number is ours.
        Assert.False(supplemented.Player.IsSupplemented);

        f.Snapshots.Setup(s => s.GetPlayerTimeline(12, PlacementScope.OfficialOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Altale D24", ChartA, 1, 999120)
            });

        var official = await f.Saga.Handle(new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "NEWCOMER"),
            CancellationToken.None);

        Assert.Null(official!.Pumbility);
        Assert.False(official.PumbilityIsSupplemented);
    }

    [Fact]
    public async Task LinkedTagsByIdsAnswerOneTagPerAccountAndSkipTheUnlinked()
    {
        var f = Arrange(Run(2, Week2));
        var linked = Guid.NewGuid();
        var renamed = Guid.NewGuid();
        var unlinked = Guid.NewGuid();
        f.Snapshots.Setup(s => s.GetPlayersByUserIds(MixEnum.Phoenix2, It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlayerDimension(11, "NIMBUS9", null, linked, Week2),
                // Two rows for one account: the crawl saw the newer tag more recently.
                new PlayerDimension(12, "OLDTAG#1", null, renamed, Week1),
                new PlayerDimension(13, "NEWTAG#1", null, renamed, Week2)
            });

        var tags = await f.Saga.Handle(
            new GetLinkedOfficialPlayerTagsQuery(MixEnum.Phoenix2, new[] { linked, renamed, unlinked }),
            CancellationToken.None);

        Assert.Equal(2, tags.Count);
        Assert.Equal("NIMBUS9", tags[linked]);
        Assert.Equal("NEWTAG#1", tags[renamed]);
        Assert.False(tags.ContainsKey(unlinked));
    }
}
