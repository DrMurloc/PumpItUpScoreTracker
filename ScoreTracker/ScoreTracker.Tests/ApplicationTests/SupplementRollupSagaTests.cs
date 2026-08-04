using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The roll-up's contract with the rest of the mirror: it only ever adds supplemented rows,
///     it only speaks for linked public players, and re-running it replaces its own output
///     rather than stacking a second copy on the board.
/// </summary>
public sealed class SupplementRollupSagaTests
{
    private const int SnapshotId = 12;
    private const int BoardId = 5;
    private static readonly Guid ChartId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublicUser = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PrivateUser = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed record Fixture(
        Mock<IOfficialSnapshotRepository> Snapshots,
        Mock<IScoreReader> Scores,
        Mock<IOfficialRecordRepository> Records,
        SupplementRollupSaga Saga)
    {
        public List<PlacementRow> Written { get; } = new();
        public List<HighlightRow> Highlights { get; } = new();
    }

    private static Fixture Arrange(
        IEnumerable<PlayerDimension>? players = null,
        IEnumerable<User>? users = null,
        IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>? ledger = null,
        IEnumerable<PlacementRow>? official = null,
        bool hasSealed = true)
    {
        var snapshots = new Mock<IOfficialSnapshotRepository>();
        snapshots.Setup(s => s.GetLatestSealed(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasSealed
                ? new SnapshotRun(SnapshotId, DateTimeOffset.Now, DateTimeOffset.Now, false, "Sealed", 1, 1, 0, null)
                : null);
        snapshots.Setup(s => s.GetPlayers(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((players ?? new[] { Linked(1, PublicUser) }).ToArray());
        snapshots.Setup(s => s.GetBoards(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BoardDimension(BoardId, LeaderboardTypes.Chart, "Some Song S20", ChartId, "Single", 20)
            });
        snapshots.Setup(s => s.GetBoardPlacements(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<PlacementScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((official ?? Array.Empty<PlacementRow>()).ToArray());
        snapshots.Setup(s => s.GetBoardPlacements(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<PlacementScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlacementRow>());
        // The highlights pass reads the whole snapshot back; a repository never answers null.
        snapshots.Setup(s => s.GetPlacements(It.IsAny<int>(), It.IsAny<PlacementScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlacementRow>());
        snapshots.Setup(s => s.GetSeenPlayerIds(It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<PlacementScope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int>());
        snapshots.Setup(s => s.GetSealedBefore(It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SnapshotRun?)null);
        snapshots.Setup(s => s.AnySupplemented(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetVerifiedBests(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (ledger ?? new[] { (PublicUser, Best(970_000)) }).Where(l => ids.Contains(l.UserId)).ToArray());

        var stats = new Mock<IPlayerStatsReader>();
        stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerStatsRecord>());

        var userReader = new Mock<IUserReader>();
        userReader.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((users ?? new[] { Person(PublicUser, true) }).ToArray());

        var records = new Mock<IOfficialRecordRepository>();
        records.Setup(r => r.DeleteSupplementedHighlights(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var fixture = new Fixture(snapshots, scores, records, new SupplementRollupSaga(snapshots.Object,
            records.Object, scores.Object, stats.Object, userReader.Object,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<SupplementRollupSaga>.Instance));

        snapshots.Setup(s => s.WritePlacements(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<PlacementRow>>(),
                It.IsAny<CancellationToken>()))
            .Callback((int _, IReadOnlyCollection<PlacementRow> rows, CancellationToken _) =>
                fixture.Written.AddRange(rows))
            .Returns(Task.CompletedTask);
        records.Setup(r => r.WriteHighlights(It.IsAny<int>(), It.IsAny<MixEnum>(),
                It.IsAny<IReadOnlyCollection<HighlightRow>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((int _, MixEnum _, IReadOnlyCollection<HighlightRow> rows, bool _, CancellationToken _) =>
                fixture.Highlights.AddRange(rows))
            .Returns(Task.CompletedTask);

        return fixture;
    }

    private static PlayerDimension Linked(int id, Guid? userId) =>
        new(id, $"PLAYER{id}#0001", null, userId);

    private static User Person(Guid id, bool isPublic) =>
        new(id, Name.From($"user-{id:N}"), isPublic, Name.From("TAG"), new Uri("https://example.test/a.png"), null);

    private static RecordedPhoenixScore Best(int score) =>
        new(ChartId, PhoenixScore.From(score), null, false, DateTimeOffset.Now, "officialImport");

    private static ConsumeContext<RollUpSupplementedLeaderboardsCommand> Context(
        MixEnum mix = MixEnum.Phoenix2)
    {
        var ctx = new Mock<ConsumeContext<RollUpSupplementedLeaderboardsCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new RollUpSupplementedLeaderboardsCommand(mix));
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task ALinkedPublicPlayerLandsOnTheBoardBelowTheOfficialRows()
    {
        var f = Arrange(official: new[] { new PlacementRow(BoardId, 90, 1, 990_000) });

        await f.Saga.Consume(Context());

        var row = Assert.Single(f.Written);
        Assert.Equal(1, row.PlayerId);
        Assert.Equal(2, row.Place);
        Assert.True(row.IsSupplemented);
    }

    [Fact]
    public async Task APrivateAccountContributesNothing()
    {
        var f = Arrange(
            players: new[] { Linked(1, PrivateUser) },
            users: new[] { Person(PrivateUser, false) },
            ledger: new[] { (PrivateUser, Best(970_000)) });

        await f.Saga.Consume(Context());

        Assert.Empty(f.Written);
        f.Scores.Verify(s => s.GetVerifiedBests(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUnlinkedMirrorPlayerContributesNothing()
    {
        // A tag the crawl has seen but no account has ever proved. Their scores are not ours
        // to publish, and we could not attribute them anyway.
        var f = Arrange(players: new[] { Linked(1, null) });

        await f.Saga.Consume(Context());

        Assert.Empty(f.Written);
    }

    [Fact]
    public async Task TheRunClearsItsOwnPreviousOutputBeforeWritingAgain()
    {
        var f = Arrange();

        await f.Saga.Consume(Context());

        // Without this a second press stacks a duplicate board on top of the first.
        f.Snapshots.Verify(s => s.DeleteSupplementedPlacements(SnapshotId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NothingHappensBeforeTheFirstSealedSnapshot()
    {
        var f = Arrange(hasSealed: false);

        await f.Saga.Consume(Context());

        Assert.Empty(f.Written);
        f.Snapshots.Verify(s => s.DeleteSupplementedPlacements(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheHighlightsPassIsMarkedSupplementedAndNeverTouchesTheRecordBooks()
    {
        var f = Arrange();

        await f.Saga.Consume(Context());

        f.Records.Verify(r => r.WriteHighlights(SnapshotId, It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyCollection<HighlightRow>>(), true, It.IsAny<CancellationToken>()), Times.Once);
        f.Records.Verify(r => r.UpsertBoardRecords(It.IsAny<IReadOnlyCollection<BoardRecordRow>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Records.Verify(r => r.UpsertFolderRecords(It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyCollection<FolderRecordRow>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheFirstEverRollUpIsItsOwnBaselineAndStaysSilent()
    {
        var f = Arrange(official: new[] { new PlacementRow(BoardId, 90, 1, 990_000) });
        f.Snapshots.Setup(s => s.AnySupplemented(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await f.Saga.Consume(Context());

        // Rows still land — it is the celebration that is suppressed, not the board.
        Assert.NotEmpty(f.Written);
        Assert.Empty(f.Highlights);
    }

    [Fact]
    public async Task AScoreOnAChartWithNoMirroredBoardIsDropped()
    {
        var elsewhere = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var f = Arrange(ledger: new[]
        {
            (PublicUser, new RecordedPhoenixScore(elsewhere, PhoenixScore.From(980_000), null, false,
                DateTimeOffset.Now, "officialImport"))
        });

        await f.Saga.Consume(Context());

        Assert.Empty(f.Written);
    }
}
