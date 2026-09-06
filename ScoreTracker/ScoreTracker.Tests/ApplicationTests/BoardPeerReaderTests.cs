using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Who a board row belongs to, and who therefore counts as one peer
///     (docs/design/pumbility-overhaul.md D61). Every case here is one the live data holds.
/// </summary>
public sealed class BoardPeerReaderTests
{
    private const int BoardId = 7;
    private static readonly DateTimeOffset SweptAt = new(2026, 8, 30, 17, 13, 0, TimeSpan.Zero);

    private readonly Mock<IOfficialSnapshotRepository> _snapshots = new();
    private readonly Mock<IUserReader> _users = new();

    private BoardPeerReader Subject => new(_snapshots.Object, _users.Object);

    private void Board(params PlacementRow[] rows)
    {
        _snapshots.Setup(s => s.GetLatestSealed(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnapshotRun(17, SweptAt, SweptAt, false, "Sealed", 0, 0, 0, null));
        _snapshots.Setup(s => s.GetBoards(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BoardDimension(BoardId, LeaderboardTypes.Rating, PumbilityBoards.Singles, null, null, null)
            });
        _snapshots.Setup(s => s.GetBoardPlacements(17, BoardId, PlacementScope.OfficialOnly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    private void Players(params PlayerDimension[] players)
    {
        _snapshots.Setup(s => s.GetPlayersByIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(players);
    }

    private void Accounts(params User[] accounts)
    {
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                accounts.Where(a => ids.Contains(a.Id)).ToArray());
        _users.Setup(u => u.GetUsersByGameTags(It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> tags, CancellationToken _) =>
            {
                var wanted = tags.Select(t => t.Replace(" ", string.Empty))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return accounts.Where(a => a.GameTag != null
                                           && wanted.Contains(a.GameTag.Value.ToString()
                                               .Replace(" ", string.Empty))).ToArray();
            });
    }

    private static User Account(Guid id, string name, string? gameTag, bool isPublic)
    {
        return new User(id, name, isPublic, gameTag == null ? null : (Name)gameTag,
            new Uri("https://piuimages.arroweclip.se/a.png"), null);
    }

    private static PlacementRow Row(int playerId, decimal pool)
    {
        return new PlacementRow(BoardId, playerId, playerId, pool);
    }

    private static PlayerDimension Player(int id, string username, Guid? userId = null)
    {
        return new PlayerDimension(id, username, null, userId, SweptAt);
    }

    private Task<BoardPeerGroupReading?> Read(double min = 18_700, double max = 19_450)
    {
        return Subject.GetBoardPeers(MixEnum.Phoenix2, ChartType.Single, min, max, CancellationToken.None);
    }

    [Fact]
    public async Task APlayerWithNoAccountIsABoardPeer()
    {
        Board(Row(1, 18_900m));
        Players(Player(1, "CHANGWONHAM#1539"));
        Accounts();

        var group = await Read();

        var peer = Assert.Single(group!.Peers);
        Assert.Equal("CHANGWONHAM#1539", peer.Tag);
        Assert.Null(peer.AccountId);
        Assert.Equal(new[] { 1 }, peer.BoardPlayerIds);
        Assert.Equal(SweptAt, group.AsOf);
    }

    [Fact]
    public async Task ALinkedPublicAccountIsReportedAsThatAccount()
    {
        var userId = Guid.NewGuid();
        Board(Row(1, 18_900m));
        Players(Player(1, "SUNMU#7646", userId));
        Accounts(Account(userId, "SUNMU #7646", "SUNMU #7646", true));

        var group = await Read();

        Assert.Equal(userId, Assert.Single(group!.Peers).AccountId);
    }

    [Fact]
    public async Task AGameTagFindsAnAccountTheLinkColumnNeverCaught()
    {
        // The site writes NAME #1234 and the board writes NAME#1234, so an exact compare finds
        // nothing at all — this is the case that made the tag pass necessary.
        var userId = Guid.NewGuid();
        Board(Row(1, 18_900m));
        Players(Player(1, "HARRYXD#8987"));
        Accounts(Account(userId, "rodrigo villagra", "HARRYXD #8987", true));

        var group = await Read();

        Assert.Equal(userId, Assert.Single(group!.Peers).AccountId);
    }

    [Fact]
    public async Task APrivateAccountIsReportedAsABoardPlayer()
    {
        var userId = Guid.NewGuid();
        Board(Row(1, 18_900m));
        Players(Player(1, "SEBASLPB#1643", userId));
        Accounts(Account(userId, "SEBAS #2808", "SEBASLPB #1643", false));

        var group = await Read();

        var peer = Assert.Single(group!.Peers);
        Assert.Null(peer.AccountId);
        Assert.Equal("SEBASLPB#1643", peer.Tag);
    }

    [Fact]
    public async Task TwoRowsForOneAccountAreOnePeerCarryingBothIds()
    {
        var userId = Guid.NewGuid();
        Board(Row(1, 18_800m), Row(2, 19_100m));
        Players(Player(1, "EUPHO#5163", userId), Player(2, "EUPHO#6352", userId));
        Accounts(Account(userId, "Eupho", "EUPHO #6352", true));

        var group = await Read();

        var peer = Assert.Single(group!.Peers);
        Assert.Equal(new[] { 2, 1 }, peer.BoardPlayerIds);
        Assert.Equal("EUPHO#6352", peer.Tag);
        Assert.Equal(19_100d, peer.Pool);
    }

    [Fact]
    public async Task ATagTwoAccountsClaimResolvesToNeither()
    {
        Board(Row(1, 18_900m));
        Players(Player(1, "TWINS#0001"));
        Accounts(Account(Guid.NewGuid(), "one", "TWINS #0001", true),
            Account(Guid.NewGuid(), "two", "TWINS #0001", true));

        var group = await Read();

        Assert.Null(Assert.Single(group!.Peers).AccountId);
    }

    [Fact]
    public async Task PoolsOutsideTheWindowAreNotPeers()
    {
        Board(Row(1, 18_600m), Row(2, 18_900m), Row(3, 19_600m));
        Players(Player(1, "LOW#1"), Player(2, "IN#2"), Player(3, "HIGH#3"));
        Accounts();

        var group = await Read();

        Assert.Equal("IN#2", Assert.Single(group!.Peers).Tag);
    }

    [Fact]
    public async Task AMixWithNoPerTypeBoardHasNoBoardPeersRatherThanNoAnswer()
    {
        _snapshots.Setup(s => s.GetLatestSealed(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnapshotRun(16, SweptAt, SweptAt, false, "Sealed", 0, 0, 0, null));
        _snapshots.Setup(s => s.GetBoards(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BoardDimension(1, LeaderboardTypes.Rating, PumbilityBoards.Combined, null, null, null)
            });

        var group = await Subject.GetBoardPeers(MixEnum.Phoenix, ChartType.Single, 0, 30_000,
            CancellationToken.None);

        Assert.NotNull(group);
        Assert.Empty(group!.Peers);
    }

    [Fact]
    public async Task AMixThatHasNeverBeenSweptHasNoAnswerAtAll()
    {
        _snapshots.Setup(s => s.GetLatestSealed(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SnapshotRun?)null);

        Assert.Null(await Read());
    }
}
