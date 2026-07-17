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
        Mock<IOfficialRecordRepository> Records, Mock<IScoreReader> Scores, Mock<IChartRepository> Charts,
        LeaderboardHubSaga Saga);

    private static Fixture Arrange(SnapshotRun? latest, SnapshotRun? previous = null)
    {
        var snapshots = new Mock<IOfficialSnapshotRepository>();
        snapshots.Setup(s => s.GetLatestSealed(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);
        snapshots.Setup(s => s.GetSealedBefore(It.IsAny<MixEnum>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        snapshots.Setup(s => s.GetPlacementDetails(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlacementDetail>());
        snapshots.Setup(s => s.GetPlayersByIds(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.Select(id => new PlayerDimension(id, $"PLAYER{id}",
                    new Uri($"https://example.invalid/{id}.png"), null)).ToArray());
        var records = new Mock<IOfficialRecordRepository>();
        records.Setup(r => r.GetHighlights(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HighlightRow>());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreTracker.SharedKernel.Models.Chart>());
        var saga = new LeaderboardHubSaga(snapshots.Object, records.Object, scores.Object, charts.Object,
            new MemoryCache(new MemoryCacheOptions()));
        return new Fixture(snapshots, records, scores, charts, saga);
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
        f.Records.Setup(r => r.GetHighlights(2, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
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
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Pumbility(11, 1, 19412.88m),
            Pumbility(12, 2, 19205.13m),
            Chart(11, ChartA, 1, 997000, level: 26, type: "Double", boardId: 500),
            Chart(11, ChartB, 2, 993000, level: 24, boardId: 501),
            Chart(12, ChartB, 1, 995000, level: 24, boardId: 501)
        });
        f.Snapshots.Setup(s => s.GetPlacementDetails(1, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
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
        Assert.Equal(1, top.NumberOnes);
    }

    [Fact]
    public async Task PhoenixRankingsFallBackToComputedRatings()
    {
        var f = Arrange(Run(2, Week2));
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Chart(11, ChartA, 1, 995000, level: 24, boardId: 500),
            Chart(12, ChartA, 2, 970000, level: 24, boardId: 500)
        });

        var result = await f.Saga.Handle(new GetOfficialRankingsQuery(MixEnum.Phoenix),
            CancellationToken.None);

        Assert.False(result.RatingIsOfficial);
        Assert.Equal(2, result.Rankings.Count);
        Assert.Equal("PLAYER11", result.Rankings[0].Player.Username);
        Assert.True(result.Rankings[0].Rating > result.Rankings[1].Rating);
        Assert.Null(result.Rankings[0].PreviousRank);
    }

    [Fact]
    public async Task ProfileBuildsTilesHistoryAndPlacementDeltas()
    {
        var f = Arrange(Run(2, Week2), Run(1, Week1));
        var player = new PlayerDimension(11, "NIMBUS9", null, null);
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "NIMBUS9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Chart(11, ChartA, 1, 999120, level: 24, type: "Double", boardId: 500)
        });
        f.Snapshots.Setup(s => s.GetPlayerTimeline(11, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
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
    public async Task LinkedPlayersWithSparseBoardsGetSupplementedCharts()
    {
        var f = Arrange(Run(2, Week2));
        var linkedUser = Guid.NewGuid();
        var boardChart = new ChartBuilder().WithLevel(24).WithType(ChartType.Single).Build();
        var ledgerChart = new ChartBuilder().WithLevel(22).WithType(ChartType.Double).Build();
        var brokenChart = new ChartBuilder().WithLevel(23).WithType(ChartType.Single).Build();
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "NIMBUS9", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerDimension(11, "NIMBUS9", null, linkedUser));
        f.Snapshots.Setup(s => s.GetPlayerTimeline(11, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Board", boardChart.Id, 3, 991000)
        });
        f.Charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { boardChart, ledgerChart, brokenChart });
        f.Scores.Setup(s => s.GetBestScores(MixEnum.Phoenix2, linkedUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                // Already on a board — never duplicated from the ledger.
                new RecordedPhoenixScore(boardChart.Id, 991000, PhoenixPlate.SuperbGame, false, Week2),
                new RecordedPhoenixScore(ledgerChart.Id, 954321, PhoenixPlate.TalentedGame, false, Week2),
                new RecordedPhoenixScore(brokenChart.Id, 812000, PhoenixPlate.RoughGame, true, Week2)
            });

        var profile = await f.Saga.Handle(new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "NIMBUS9"),
            CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(2, profile!.Placements.Count);
        var board = profile.Placements.Single(p => p.ChartId == boardChart.Id);
        Assert.False(board.Supplemented);
        Assert.Equal(3, board.Place);
        var supplemented = profile.Placements.Single(p => p.ChartId == ledgerChart.Id);
        Assert.True(supplemented.Supplemented);
        Assert.Null(supplemented.Place);
        Assert.Equal(954321, supplemented.Score);
        Assert.True(supplemented.ComputedRating > 0);
        // The broken play never supplements, and the stat tiles stay board-only.
        Assert.DoesNotContain(profile.Placements, p => p.ChartId == brokenChart.Id);
        Assert.Equal(1, profile.BoardsInTop);
    }

    [Fact]
    public async Task UnlinkedPlayersNeverSupplement()
    {
        var f = Arrange(Run(2, Week2));
        var boardChart = new ChartBuilder().WithLevel(24).WithType(ChartType.Single).Build();
        f.Snapshots.Setup(s => s.GetPlayerByUsername(MixEnum.Phoenix2, "GHOST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerDimension(12, "GHOST", null, null));
        f.Snapshots.Setup(s => s.GetPlayerTimeline(12, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new PlayerTimelineRow(2, Week2, LeaderboardTypes.Chart, "Board", boardChart.Id, 5, 960000)
        });

        var profile = await f.Saga.Handle(new GetOfficialPlayerProfileQuery(MixEnum.Phoenix2, "GHOST"),
            CancellationToken.None);

        Assert.Single(profile!.Placements);
        f.Scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullBoard);
        f.Snapshots.Setup(s => s.GetPlacementDetails(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousBoard);
        f.Snapshots.Setup(s => s.GetBoardFloorHistory(MixEnum.Phoenix2, "PUMBILITY",
                It.IsAny<CancellationToken>()))
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
        Assert.Equal(3, result.Boards.Count);
        Assert.Equal(13000m, result.Boards.Single(b => b.Type == "All").EntryValue);
        Assert.False(result.Boards.Single(b => b.Type == "Singles").BoardFull);
    }

    [Fact]
    public async Task WhatItTakesOnAnUnfullBoardHasNoEntryButStillLaddersWhatExists()
    {
        var f = Arrange(Run(2, Week2));
        var partialBoard = Enumerable.Range(1, 250)
            .Select(rank => Pumbility(rank, rank, 18000m - rank * 10))
            .ToArray();
        f.Snapshots.Setup(s => s.GetPlacementDetails(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partialBoard);
        f.Snapshots.Setup(s => s.GetBoardFloorHistory(MixEnum.Phoenix2, "PUMBILITY",
                It.IsAny<CancellationToken>()))
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
}
