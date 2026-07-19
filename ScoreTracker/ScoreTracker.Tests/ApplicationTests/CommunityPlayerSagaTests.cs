using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
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
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IPlayerStatsReader> _playerStats = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IUserReader> _users = new();

    private CommunityPlayerSaga Build()
    {
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(Caller).Build());
        return new CommunityPlayerSaga(_communities.Object, _currentUser.Object, _scores.Object,
            _playerStats.Object, _charts.Object, _users.Object);
    }

    private void GivenCommunity(CommunityPrivacyType privacy, params Guid[] members)
    {
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), privacy, members,
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false);
        _communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);
    }

    private void GivenTargetData(Chart[] charts, RecordedPhoenixScore[] scores)
    {
        _users.Setup(u => u.GetUser(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(Target).WithName("Target").Build());
        _playerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Target, 12000, 22, 500, 0, 0, 900, 950000, 20.5,
                871, 960000, 20.9, 852, 940000, 20.1, 20.6, 20.8, 20.2));
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    [Fact]
    public async Task ProfileRequiresMembershipForPrivateCommunities()
    {
        GivenCommunity(CommunityPrivacyType.Private, Target);

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() => Build().Handle(
            new GetCommunityPlayerProfileQuery(Name.From("Acme"), Target, MixEnum.Phoenix),
            CancellationToken.None));
    }

    [Fact]
    public async Task ProfileRequiresTheSubjectToBeAMember()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller);

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() => Build().Handle(
            new GetCommunityPlayerProfileQuery(Name.From("Acme"), Target, MixEnum.Phoenix),
            CancellationToken.None));
    }

    [Fact]
    public async Task ProfileProjectsStatsAndFolderCompletion()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        var passed = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var unpassed = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var coOp = new ChartBuilder().WithLevel(2).WithType(ChartType.CoOp).Build();
        GivenTargetData(new[] { passed, unpassed, coOp }, new[]
        {
            new RecordedPhoenixScore(passed.Id, 990000, PhoenixPlate.SuperbGame, false, Now)
        });

        var profile = await Build().Handle(
            new GetCommunityPlayerProfileQuery(Name.From("Acme"), Target, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(900, profile!.Pumbility);
        Assert.Equal(20.6, profile.CompetitiveLevel);
        // The level-20 folder counts 1 doubles pass of 2 charts; the co-op chart stays out.
        var folder = profile.FolderCompletion.Single(f => f.Level == 20);
        Assert.Equal(0, folder.SinglesPassed);
        Assert.Equal(1, folder.DoublesPassed);
        Assert.Equal(2, folder.Total);
        Assert.DoesNotContain(profile.FolderCompletion, f => f.Level == 2);
    }

    [Fact]
    public async Task ComparisonPairsBothPlayersScoresPerChart()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        var contested = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var theirsOnly = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contested, theirsOnly });
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), Caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RecordedPhoenixScore(contested.Id, 991300, null, false, Now) });
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(contested.Id, 974660, null, false, Now),
                new RecordedPhoenixScore(theirsOnly.Id, 955900, null, false, Now)
            });

        var rows = (await Build().Handle(
                new GetCommunityFolderComparisonQuery(Name.From("Acme"), Target, ChartType.Double, 20,
                    MixEnum.Phoenix), CancellationToken.None))
            .ToDictionary(r => r.ChartId);

        Assert.Equal(991300, rows[contested.Id].MyScore);
        Assert.Equal(974660, rows[contested.Id].TheirScore);
        Assert.Null(rows[theirsOnly.Id].MyScore);
        Assert.Equal(955900, rows[theirsOnly.Id].TheirScore);
    }

    [Fact]
    public async Task PrivateProfilesAreInvisibleToViewersOutsideTheCommunity()
    {
        // The target is a private-profile member of a public community; the caller is NOT a
        // member — membership is the visibility consent, so the profile read is denied.
        GivenCommunity(CommunityPrivacyType.Public, Target);
        _users.Setup(u => u.GetUser(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(Target).WithIsPublic(false).Build());

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() => Build().Handle(
            new GetCommunityPlayerProfileQuery(Name.From("Acme"), Target, MixEnum.Phoenix),
            CancellationToken.None));
    }

    [Fact]
    public async Task PrivateProfilesAreVisibleToFellowMembers()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        GivenTargetData(new[] { chart }, Array.Empty<RecordedPhoenixScore>());
        _users.Setup(u => u.GetUser(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(Target).WithIsPublic(false).Build());

        var profile = await Build().Handle(
            new GetCommunityPlayerProfileQuery(Name.From("Acme"), Target, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.NotNull(profile);
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
    public async Task ComparisonRequiresLogin()
    {
        GivenCommunity(CommunityPrivacyType.Public, Caller, Target);
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        var saga = new CommunityPlayerSaga(_communities.Object, _currentUser.Object, _scores.Object,
            _playerStats.Object, _charts.Object, _users.Object);

        await Assert.ThrowsAsync<UserNotLoggedInException>(() => saga.Handle(
            new GetCommunityFolderComparisonQuery(Name.From("Acme"), Target, ChartType.Double, 20,
                MixEnum.Phoenix), CancellationToken.None));
    }
}
