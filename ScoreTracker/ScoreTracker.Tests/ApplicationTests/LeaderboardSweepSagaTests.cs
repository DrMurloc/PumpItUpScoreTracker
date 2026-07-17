using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class LeaderboardSweepSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 16, 30, 0, TimeSpan.Zero);
    private const int SnapshotId = 77;

    private sealed record Fixture(
        Mock<IOfficialSiteClient> Site,
        Mock<IOfficialSnapshotRepository> Snapshots,
        Mock<IOfficialRecordRepository> Records,
        Mock<ITierListRepository> TierLists,
        LeaderboardSweepSaga Saga)
    {
        public Mock<IOfficialLeaderboardRepository> Legacy { get; init; } = null!;
        public Mock<IChartRepository> Charts { get; init; } = null!;
    }

    private static Fixture Arrange(
        IEnumerable<RatingBoardEntry>? ratingBoards = null,
        IEnumerable<OfficialChartBoardResult>? chartBoards = null,
        IEnumerable<ChartPopularityLeaderboardEntry>? popularity = null,
        bool hasSealed = true,
        bool unsealedInFlight = false)
    {
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.GetRatingBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratingBoards ?? Array.Empty<RatingBoardEntry>());
        site.Setup(s => s.GetOfficialChartBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsync(chartBoards ?? Array.Empty<OfficialChartBoardResult>()));
        site.Setup(s => s.GetOfficialChartLeaderboardEntries(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(popularity ?? Array.Empty<ChartPopularityLeaderboardEntry>());

        var snapshots = new Mock<IOfficialSnapshotRepository>();
        snapshots.Setup(s => s.AnySealed(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasSealed);
        snapshots.Setup(s => s.HasLiveRun(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(unsealedInFlight);
        snapshots.Setup(s => s.CreateRun(It.IsAny<MixEnum>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(SnapshotId);
        var boardIds = 0;
        snapshots.Setup(s => s.EnsureBoard(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, string type, string name, Guid? chartId, string? chartType, int? level,
                    CancellationToken _) =>
                new BoardDimension(++boardIds, type, name, chartId, chartType, level));
        var playerIds = 0;
        var knownPlayers = new Dictionary<string, PlayerDimension>(StringComparer.OrdinalIgnoreCase);
        snapshots.Setup(s => s.EnsurePlayers(It.IsAny<MixEnum>(),
                It.IsAny<IReadOnlyCollection<(string Username, Uri? Avatar)>>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IReadOnlyCollection<(string Username, Uri? Avatar)> players,
                DateTimeOffset _, CancellationToken _) =>
            {
                foreach (var (username, avatar) in players)
                    if (knownPlayers.TryGetValue(username, out var existing))
                        knownPlayers[username] = existing with { Avatar = avatar ?? existing.Avatar };
                    else
                        knownPlayers[username] = new PlayerDimension(++playerIds, username, avatar, null);
                return (IReadOnlyList<PlayerDimension>)players
                    .Select(p => knownPlayers[p.Username]).ToArray();
            });

        snapshots.Setup(s => s.GetBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardDimension>());
        snapshots.Setup(s => s.GetPlacements(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlacementRow>());
        snapshots.Setup(s => s.GetSealedBefore(It.IsAny<MixEnum>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((SnapshotRun?)null);

        snapshots.Setup(s => s.GetPlayers(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerDimension>());

        var records = new Mock<IOfficialRecordRepository>();
        records.Setup(r => r.GetBoardRecords(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardRecordRow>());
        records.Setup(r => r.GetFolderRecords(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FolderRecordRow>());
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var legacy = new Mock<IOfficialLeaderboardRepository>();
        legacy.Setup(l => l.GetAllEntries(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserOfficialLeaderboard>());
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var tierLists = new Mock<ITierListRepository>();
        var saga = new LeaderboardSweepSaga(site.Object, snapshots.Object, records.Object, identity.Object,
            legacy.Object, charts.Object, tierLists.Object, FakeDateTime.At(Now).Object,
            NullLogger<LeaderboardSweepSaga>.Instance);
        return new Fixture(site, snapshots, records, tierLists, saga) { Legacy = legacy, Charts = charts };
    }

    private static async IAsyncEnumerable<OfficialChartBoardResult> ToAsync(
        IEnumerable<OfficialChartBoardResult> boards,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var board in boards)
        {
            yield return board;
            await Task.Yield();
        }
    }

    private static ConsumeContext<StartLeaderboardImportCommand> Context(MixEnum mix = MixEnum.Phoenix2)
    {
        var ctx = new Mock<ConsumeContext<StartLeaderboardImportCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new StartLeaderboardImportCommand(mix));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task SweepSealsTheSnapshotAfterAllStages()
    {
        var f = Arrange();

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.Seal(SnapshotId, Now, It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.MarkFailed(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SweepMarksTheRunFailedAndNeverSealsWhenAStageThrows()
    {
        var f = Arrange();
        f.Site.Setup(s => s.GetRatingBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("site down"));

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.MarkFailed(SnapshotId, It.Is<string>(e => e.Contains("site down")),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.Seal(It.IsAny<int>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SweepSkipsEntirelyWhenARunIsAlreadyInFlight()
    {
        var f = Arrange(unsealedInFlight: true);

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.CreateRun(It.IsAny<MixEnum>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        // The guard only respects runs with a fresh heartbeat — a killed run stops beating
        // and releases the lock in minutes, so an interrupted sweep can be re-triggered.
        f.Snapshots.Verify(s => s.HasLiveRun(MixEnum.Phoenix2, Now - TimeSpan.FromMinutes(15),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FirstSweepForAMixRunsAsBaseline()
    {
        var f = Arrange(hasSealed: false);

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.CreateRun(MixEnum.Phoenix2, true, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RatingBoardsWriteOlympicPlacementsWithDecimalValues()
    {
        var f = Arrange(ratingBoards: new[]
        {
            new RatingBoardEntry("PUMBILITY", "alice", 17418.45m),
            new RatingBoardEntry("PUMBILITY", "bob", 17418.45m),
            new RatingBoardEntry("PUMBILITY", "carol", 16000.10m)
        });
        IReadOnlyCollection<PlacementRow>? captured = null;
        f.Snapshots.Setup(s => s.WritePlacements(SnapshotId, It.IsAny<IReadOnlyCollection<PlacementRow>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<PlacementRow>, CancellationToken>((_, rows, _) => captured = rows)
            .Returns(Task.CompletedTask);

        await f.Saga.Consume(Context());

        Assert.NotNull(captured);
        var places = captured!.OrderBy(r => r.PlayerId).Select(r => r.Place).ToArray();
        Assert.Equal(new[] { 1, 1, 3 }, places);
        Assert.Equal(17418.45m, captured!.First().Score);
    }

    [Fact]
    public async Task ChartBoardsWritePlacementsCarryTheChartOnTheBoardDimension()
    {
        var chart = new ChartBuilder().WithLevel(26).WithType(ChartType.Double).Build();
        var f = Arrange(chartBoards: new[]
        {
            new OfficialChartBoardResult(1, 1, chart, null, new[]
            {
                new OfficialChartLeaderboardEntry("alice", chart, 997342,
                    new Uri("https://example.invalid/alice.png")),
                new OfficialChartLeaderboardEntry("bob", chart, 990000,
                    new Uri("https://example.invalid/bob.png"))
            })
        });

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.EnsureBoard(MixEnum.Phoenix2, LeaderboardTypes.Chart,
            It.Is<string>(n => n.Contains(chart.Song.Name)), chart.Id, chart.Type.ToString(), (int)chart.Level,
            It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.WritePlacements(SnapshotId,
            It.Is<IReadOnlyCollection<PlacementRow>>(rows => rows.Count == 2 &&
                                                             rows.Any(r => r.Place == 1 && r.Score == 997342)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SkippedBoardsCountWithoutKillingTheSweep()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Single).Build();
        var f = Arrange(chartBoards: new[]
        {
            new OfficialChartBoardResult(1, 2, null, "no catalog chart: Mystery S24",
                Array.Empty<OfficialChartLeaderboardEntry>()),
            new OfficialChartBoardResult(2, 2, chart, null, new[]
            {
                new OfficialChartLeaderboardEntry("alice", chart, 950000,
                    new Uri("https://example.invalid/alice.png"))
            })
        });

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.UpdateProgress(SnapshotId, "ChartBoards", 2, 1, 1,
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        f.Snapshots.Verify(s => s.Seal(SnapshotId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PopularityWritesRankedRowsOnlyButCategorizesEverything()
    {
        var ranked = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var unranked = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var f = Arrange(popularity: new[]
        {
            new ChartPopularityLeaderboardEntry(ranked, 5, new Uri("https://example.invalid/a.png")),
            new ChartPopularityLeaderboardEntry(unranked, -1, new Uri("https://example.invalid/b.png"))
        });

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.WritePopularity(SnapshotId,
            It.Is<IReadOnlyCollection<(Guid ChartId, int Place)>>(rows =>
                rows.Count == 1 && rows.First().ChartId == ranked.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        f.TierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix2,
            It.Is<SongTierListEntry>(e => e.ChartId == unranked.Id && e.Category == TierListCategory.Unrecorded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChartBoardScoresStillFeedTheOfficialScoresTierList()
    {
        var chart = new ChartBuilder().WithLevel(22).WithType(ChartType.Single).Build();
        var f = Arrange(chartBoards: new[]
        {
            new OfficialChartBoardResult(1, 1, chart, null, new[]
            {
                new OfficialChartLeaderboardEntry("alice", chart, 970000,
                    new Uri("https://example.invalid/alice.png"))
            })
        });

        await f.Saga.Consume(Context());

        f.TierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix2,
            It.Is<SongTierListEntry>(e =>
                (string)e.TierListName == "Official Scores" && e.ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LastImportTimestampReadsTheLatestSeal()
    {
        var f = Arrange();
        f.Snapshots.Setup(s => s.GetLatestSealed(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnapshotRun(3, Now.AddDays(-7), Now.AddDays(-7).AddMinutes(40), false, "Sealed",
                600, 598, 2, null));

        var result = await f.Saga.Handle(new GetLastLeaderboardImportTimestampQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Equal(Now.AddDays(-7).AddMinutes(40), result);
    }

    private static ConsumeContext<SeedBaselineSnapshotCommand> SeedContext(MixEnum mix = MixEnum.Phoenix2)
    {
        var ctx = new Mock<ConsumeContext<SeedBaselineSnapshotCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new SeedBaselineSnapshotCommand(mix));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task SeedBuildsASealedBaselineFromLegacyRowsWithChartAssociation()
    {
        var chart = new ChartBuilder().WithLevel(26).WithType(ChartType.Double).Build();
        var boardName = chart.Song.Name + " " + chart.DifficultyString;
        var f = Arrange(hasSealed: false);
        f.Legacy.Setup(l => l.GetAllEntries(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserOfficialLeaderboard("alice", 1, "Chart", boardName, 997000),
                new UserOfficialLeaderboard("bob", 2, "Chart", boardName, 990000),
                new UserOfficialLeaderboard("alice", 4, "Rating", "PUMBILITY", 17418)
            });
        f.Charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        await f.Saga.Consume(SeedContext());

        f.Snapshots.Verify(s => s.CreateRun(MixEnum.Phoenix2, true, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        f.Snapshots.Verify(s => s.EnsureBoard(MixEnum.Phoenix2, "Chart", boardName, chart.Id,
            chart.Type.ToString(), (int)chart.Level, It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.EnsureBoard(MixEnum.Phoenix2, "Rating", "PUMBILITY", null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.WritePlacements(SnapshotId,
            It.Is<IReadOnlyCollection<PlacementRow>>(rows =>
                rows.Count == 2 && rows.Any(r => r.Place == 1 && r.Score == 997000)),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.Seal(SnapshotId, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeedRefusesOnceASealedSnapshotExists()
    {
        var f = Arrange(hasSealed: true);

        await f.Saga.Consume(SeedContext());

        f.Snapshots.Verify(s => s.CreateRun(It.IsAny<MixEnum>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PopularityRefreshReattachesToTheLatestSealedSnapshot()
    {
        var ranked = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var f = Arrange(popularity: new[]
        {
            new ChartPopularityLeaderboardEntry(ranked, 3, new Uri("https://example.invalid/a.png"))
        });
        f.Snapshots.Setup(s => s.GetLatestSealed(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnapshotRun(41, Now.AddMinutes(-90), Now.AddMinutes(-50), false, "Sealed",
                600, 600, 0, null));
        var refresh = new Mock<ConsumeContext<RefreshPopularityCommand>>();
        refresh.SetupGet(c => c.Message).Returns(new RefreshPopularityCommand(MixEnum.Phoenix2));
        refresh.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await f.Saga.Consume(refresh.Object);

        // Old rows clear first so a repeat press cannot collide on the placement key.
        f.Snapshots.Verify(s => s.DeletePopularity(41, It.IsAny<CancellationToken>()), Times.Once);
        f.Snapshots.Verify(s => s.WritePopularity(41,
            It.Is<IReadOnlyCollection<(Guid ChartId, int Place)>>(rows =>
                rows.Count == 1 && rows.First().ChartId == ranked.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        f.TierLists.Verify(t => t.SaveEntry(MixEnum.Phoenix2,
            It.Is<SongTierListEntry>(e => (string)e.TierListName == "Popularity"),
            It.IsAny<CancellationToken>()), Times.Once);
        // No new run is created and nothing re-seals — the snapshot is only enriched.
        f.Snapshots.Verify(s => s.CreateRun(It.IsAny<MixEnum>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Snapshots.Verify(s => s.Seal(It.IsAny<int>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PopularityRefreshWithoutASealedSnapshotDoesNothing()
    {
        var f = Arrange();
        f.Snapshots.Setup(s => s.GetLatestSealed(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SnapshotRun?)null);
        var refresh = new Mock<ConsumeContext<RefreshPopularityCommand>>();
        refresh.SetupGet(c => c.Message).Returns(new RefreshPopularityCommand(MixEnum.Phoenix2));
        refresh.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await f.Saga.Consume(refresh.Object);

        f.Snapshots.Verify(s => s.DeletePopularity(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Snapshots.Verify(s => s.WritePopularity(It.IsAny<int>(),
            It.IsAny<IReadOnlyCollection<(Guid ChartId, int Place)>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StaleUnsealedRunsPurgeBeforeANewRunStarts()
    {
        var f = Arrange();

        await f.Saga.Consume(Context());

        f.Snapshots.Verify(s => s.PurgeUnsealed(MixEnum.Phoenix2, Now - TimeSpan.FromDays(7),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
