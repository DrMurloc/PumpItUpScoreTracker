using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TitleCommunityHandlerTests
{
    private readonly Mock<IPlayerStatsReader> _stats = new();
    private readonly Mock<ITitleRepository> _titles = new();
    private readonly Mock<IUserReader> _users = new();

    private TitleCommunityHandler Handler => new(_titles.Object, _users.Object, _stats.Object);

    private static User Player(string name, bool isPublic = true)
    {
        return new User(Guid.NewGuid(), name, isPublic, name, new Uri("https://example.test/p.png"), "US");
    }

    private void HasHolders(params (User user, ParagonLevel paragon)[] holders)
    {
        _titles.Setup(t => t.GetUsersWithTitle(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holders.Select(h => new TitleAchievedRecord(h.user.Id, "The Master", h.paragon)));
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holders.Select(h => h.user));
    }

    private void HasPools(params (Guid userId, double pool)[] pools)
    {
        _stats.Setup(s => s.GetStats(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pools.Select(p => new PlayerStatsRecord(p.userId, 12000, 22, 500, 0, 0, p.pool,
                950000, 20.5, 871, 960000, 20.9, 852, 940000, 20.1, 20.6, 20.8, 20.2)));
    }

    [Fact]
    public async Task RarityReportsHolderCountsAgainstTheTrackedPopulation()
    {
        _titles.Setup(t => t.GetTitleAggregations(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TitleAggregationRecord("The Master", 8),
                new TitleAggregationRecord("Advanced Lv. 7", 142)
            });
        _titles.Setup(t => t.CountTitledUsers(It.IsAny<CancellationToken>())).ReturnsAsync(1562);

        var result = await Handler.Handle(new GetTitleRarityQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(1562, result.TrackedPlayers);
        Assert.Equal(8, result.Holders[(Name)"The Master"]);
        Assert.Equal(8 / 1562.0, result.ShareOf("The Master"));
    }

    [Fact]
    public void ATitleNobodyHoldsSharesZeroRatherThanThrowing()
    {
        var result = new TitleRarityRecord(new Dictionary<Name, int>(), 1562);

        Assert.Equal(0, result.ShareOf("SPECIALIST"));
    }

    [Fact]
    public void RarityNeverDividesByAnEmptyPopulation()
    {
        // A fresh database has no titled players at all; the page still renders.
        var result = new TitleRarityRecord(new Dictionary<Name, int> { [(Name)"The Master"] = 3 }, 0);

        Assert.Equal(0, result.ShareOf("The Master"));
    }

    [Fact]
    public async Task HoldersComeBackByName()
    {
        HasHolders(
            (Player("Zephyr"), ParagonLevel.AA),
            (Player("Alpha"), ParagonLevel.PG),
            (Player("Mid"), ParagonLevel.AA));

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix, "The Master"),
            CancellationToken.None);

        Assert.Equal(new[] { "Alpha", "Mid", "Zephyr" }, result.Holders.Select(h => h.User.Name.ToString()));
    }

    [Fact]
    public async Task ANonGemTitleNeverPaysTheStatsRead()
    {
        HasHolders((Player("Anyone"), ParagonLevel.AA));

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix, "The Master"),
            CancellationToken.None);

        Assert.All(result.Holders, h => Assert.Null(h.TotalPumbility));
        _stats.Verify(
            s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AGemRungCarriesEachHoldersPoolStrongestFirst()
    {
        var mid = Player("Mid");
        var top = Player("Top");
        var low = Player("Low");
        HasHolders((mid, ParagonLevel.None), (top, ParagonLevel.None), (low, ParagonLevel.None));
        HasPools((mid.Id, 17_641.20), (top.Id, 17_791.55), (low.Id, 17_612.03));

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix2, "[P.B] DIAMOND"),
            CancellationToken.None);

        Assert.Equal(new[] { "Top", "Mid", "Low" }, result.Holders.Select(h => h.User.Name.ToString()));
        Assert.Equal(new double?[] { 17_791.55, 17_641.20, 17_612.03 },
            result.Holders.Select(h => h.TotalPumbility));
    }

    /// <summary>
    ///     A titles row can exist for an account whose stats row does not (or not yet) — the holder
    ///     stays listed, just without a number, and sorts after everyone who has one.
    /// </summary>
    [Fact]
    public async Task AHolderWithoutAStatsRowStaysListedWithoutAPool()
    {
        var known = Player("Known");
        var statless = Player("Statless");
        HasHolders((statless, ParagonLevel.None), (known, ParagonLevel.None));
        HasPools((known.Id, 17_100.5));

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix2, "[P.B] DIAMOND"),
            CancellationToken.None);

        Assert.Equal(new[] { "Known", "Statless" }, result.Holders.Select(h => h.User.Name.ToString()));
        Assert.Null(result.Holders[1].TotalPumbility);
    }

    [Fact]
    public async Task APrivateHolderIsCountedButNeverNamed()
    {
        HasHolders(
            (Player("Public"), ParagonLevel.A),
            (Player("Hidden", false), ParagonLevel.PG),
            (Player("AlsoHidden", false), ParagonLevel.S));

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix, "The Master"),
            CancellationToken.None);

        Assert.Equal(new[] { "Public" }, result.Holders.Select(h => h.User.Name.ToString()));
        Assert.Equal(2, result.HiddenCount);
    }

    [Fact]
    public async Task OnALadderOnlyThePlayersStandingOnTheRungAreListed()
    {
        // Intermediate Lv.1 is held by nearly everyone, so listing every holder says nothing.
        // The ones who matter are the ones it is still their furthest rung.
        var standing = Player("Standing");
        var climber = Player("Climber");
        var ladder = new Name[] { "Intermediate Lv. 1", "Intermediate Lv. 2", "Intermediate Lv. 3" };

        _titles.Setup(t => t.GetUsersWithTitles(MixEnum.Phoenix, It.IsAny<IEnumerable<Name>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TitleAchievedRecord(standing.Id, "Intermediate Lv. 1", ParagonLevel.None),
                new TitleAchievedRecord(climber.Id, "Intermediate Lv. 1", ParagonLevel.None),
                new TitleAchievedRecord(climber.Id, "Intermediate Lv. 3", ParagonLevel.None)
            });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { standing });

        var result = await Handler.Handle(
            new GetTitleHoldersQuery(MixEnum.Phoenix, "Intermediate Lv. 1", ladder), CancellationToken.None);

        Assert.Equal(new[] { "Standing" }, result.Holders.Select(h => h.User.Name.ToString()));
        Assert.Equal(1, result.ClimbedPastCount);
    }

    [Fact]
    public async Task ClimbingPastCountsOnceHoweverManyRungsAbovePlayerHolds()
    {
        var climber = Player("Climber");
        var ladder = new Name[] { "Advanced Lv. 1", "Advanced Lv. 2", "Advanced Lv. 3" };

        _titles.Setup(t => t.GetUsersWithTitles(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Name>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TitleAchievedRecord(climber.Id, "Advanced Lv. 1", ParagonLevel.None),
                new TitleAchievedRecord(climber.Id, "Advanced Lv. 2", ParagonLevel.None),
                new TitleAchievedRecord(climber.Id, "Advanced Lv. 3", ParagonLevel.None)
            });

        var result = await Handler.Handle(
            new GetTitleHoldersQuery(MixEnum.Phoenix, "Advanced Lv. 1", ladder), CancellationToken.None);

        Assert.Empty(result.Holders);
        Assert.Equal(1, result.ClimbedPastCount);
    }

    [Fact]
    public async Task TheTopRungOfALadderCountsNobodyAsHavingClimbedPastIt()
    {
        var top = Player("Top");
        var ladder = new Name[] { "Advanced Lv. 1", "Advanced Lv. 2" };
        _titles.Setup(t => t.GetUsersWithTitle(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TitleAchievedRecord(top.Id, "Advanced Lv. 2", ParagonLevel.None) });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { top });

        var result = await Handler.Handle(
            new GetTitleHoldersQuery(MixEnum.Phoenix, "Advanced Lv. 2", ladder), CancellationToken.None);

        Assert.Single(result.Holders);
        Assert.Equal(0, result.ClimbedPastCount);
        // Nothing above it to compare against, so it takes the cheaper single-title read.
        _titles.Verify(t => t.GetUsersWithTitles(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Name>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ATitleWithNoHoldersSkipsTheUserLookupEntirely()
    {
        _titles.Setup(t => t.GetUsersWithTitle(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TitleAchievedRecord>());

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix2, "SPECIALIST"),
            CancellationToken.None);

        Assert.Empty(result.Holders);
        Assert.Equal(0, result.HiddenCount);
        _users.Verify(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AHolderWhoseAccountIsGoneDropsOutInsteadOfCrashing()
    {
        // Achievement rows outlive account purges; the drawer must survive the gap.
        var present = Player("Present");
        _titles.Setup(t => t.GetUsersWithTitle(It.IsAny<MixEnum>(), It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TitleAchievedRecord(present.Id, "The Master", ParagonLevel.A),
                new TitleAchievedRecord(Guid.NewGuid(), "The Master", ParagonLevel.PG)
            });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { present });

        var result = await Handler.Handle(new GetTitleHoldersQuery(MixEnum.Phoenix, "The Master"),
            CancellationToken.None);

        Assert.Single(result.Holders);
        Assert.Equal(1, result.HiddenCount);
    }
}
