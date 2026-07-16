using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
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
