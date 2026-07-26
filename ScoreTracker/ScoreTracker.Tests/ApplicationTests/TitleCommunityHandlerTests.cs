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
    private readonly Mock<ITitleRepository> _titles = new();
    private readonly Mock<IUserReader> _users = new();

    private TitleCommunityHandler Handler => new(_titles.Object, _users.Object);

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
