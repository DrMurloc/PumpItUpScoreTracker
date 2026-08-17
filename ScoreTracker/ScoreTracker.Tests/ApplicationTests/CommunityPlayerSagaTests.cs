using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunityPlayerSagaTests
{
    private static readonly Guid Caller = Guid.NewGuid();
    private static readonly Guid Target = Guid.NewGuid();

    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IChartRepository> _charts = new();

    private CommunityPlayerSaga Build()
    {
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(Caller).Build());
        return new CommunityPlayerSaga(_communities.Object, _currentUser.Object, _scores.Object, _charts.Object);
    }

    private void GivenCommunity(CommunityPrivacyType privacy, params Guid[] members)
    {
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), privacy, members,
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false);
        _communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
    }

    [Fact]
    public async Task CoOpCompletionPoolsEveryPlayerCountFolder()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        var duo = new ChartBuilder().WithLevel(2).WithType(ChartType.CoOp).Build();
        var trio = new ChartBuilder().WithLevel(3).WithType(ChartType.CoOp).Build();
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                ChartType.CoOp, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { duo, trio });
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                ChartType.CoOp, It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                ChartType.CoOp, It.Is<DifficultyLevel>(d => (int)d == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (Target, new RecordedPhoenixScore(duo.Id, 950000, null, false, DateTimeOffset.MinValue))
            });

        var completion = await Build().Handle(
            new GetCommunityCoOpCompletionQuery(Name.From("Acme"), MixEnum.Phoenix), CancellationToken.None);

        // One of the two co-op charts passed, ×2–×5 pooled into a single figure.
        Assert.Equal(0.5, completion[Target]);
    }

    [Fact]
    public async Task PlayCountsComeFromTheFullJournalForTheMemberSet()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        _scores.Setup(s => s.GetJournaledChartCounts(MixEnum.Phoenix,
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(Target) && ids.Contains(Caller)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [Target] = 812 });

        var counts = await Build().Handle(
            new GetCommunityPlayCountsQuery(Name.From("Acme"), MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(812, counts[Target]);
    }

    [Fact]
    public async Task APrivateCommunityAnswersItsAggregatesToMembersOnly()
    {
        GivenCommunity(CommunityPrivacyType.Private, Target);

        await Assert.ThrowsAsync<Domain.Exceptions.DeniedFromCommunityException>(() => Build().Handle(
            new GetCommunityPlayCountsQuery(Name.From("Acme"), MixEnum.Phoenix), CancellationToken.None));
    }
}
