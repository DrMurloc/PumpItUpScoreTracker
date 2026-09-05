using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScoreQualitySagaTests
{
    private static PlayerStatsRecord Stats(Guid userId, double singlesCompetitive = 20, double doublesCompetitive = 20)
        => new(userId, (Rating)0, DifficultyLevel.From(20), 0, (Rating)0, (PhoenixScore)0, (Rating)0, (PhoenixScore)0,
            0, (Rating)0, (PhoenixScore)0, 0, (Rating)0, (PhoenixScore)0, 0, 0, singlesCompetitive,
            doublesCompetitive);

    private static (Mock<ICurrentUserAccessor>, Guid) UserAccessor()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build();
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.User).Returns(user);
        accessor.SetupGet(a => a.IsLoggedIn).Returns(true);
        return (accessor, userId);
    }

    [Fact]
    public async Task GetCompetitivePlayersDelegatesToPlayerStatsRepositoryWithCompetitiveBand()
    {
        var (accessor, userId) = UserAccessor();
        var playerStats = new Mock<IPlayerStatsReader>();
        var scores = new Mock<IScoreReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId, singlesCompetitive: 17.5));

        var competitors = new[] { Guid.NewGuid(), Guid.NewGuid() };
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, 17.5, .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(competitors);

        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object,
            new CohortScoreProvider(playerStats.Object, scores.Object, cache));

        var result = await saga.Handle(new GetCompetitivePlayersQuery(ChartType.Single), CancellationToken.None);

        Assert.Equal(competitors, result);
    }

    /// <summary>
    ///     Another player's sessions page opens THEIR band from a peer line, so the query takes a
    ///     subject and the stats read is theirs, not the viewer's.
    /// </summary>
    [Fact]
    public async Task TheBandIsTheSubjectsWhenAHostNamesOne()
    {
        var (accessor, viewer) = UserAccessor();
        var subject = Guid.NewGuid();
        var playerStats = new Mock<IPlayerStatsReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(subject, singlesCompetitive: 19));
        var competitors = new[] { Guid.NewGuid() };
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, 19, .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(competitors);
        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object,
            new CohortScoreProvider(playerStats.Object, Mock.Of<IScoreReader>(), cache));

        var result = await saga.Handle(new GetCompetitivePlayersQuery(ChartType.Single, Subject: subject),
            CancellationToken.None);

        Assert.Equal(competitors, result);
        playerStats.Verify(p => p.GetStats(MixEnum.Phoenix, viewer, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Logged out and no subject named: no band, and no reach for a user that is not there.</summary>
    [Fact]
    public async Task LoggedOutWithNoSubjectThereIsNoBand()
    {
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.IsLoggedIn).Returns(false);
        var playerStats = new Mock<IPlayerStatsReader>();
        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object,
            new CohortScoreProvider(playerStats.Object, Mock.Of<IScoreReader>(), new MemoryCache(new MemoryCacheOptions())));

        var result = await saga.Handle(new GetCompetitivePlayersQuery(ChartType.Single), CancellationToken.None);

        Assert.Empty(result);
        playerStats.Verify(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
