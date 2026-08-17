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
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerProfileHandlerTests
{
    private static readonly Guid Caller = Guid.NewGuid();
    private static readonly Guid Target = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IPlayerStatsReader> _playerStats = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly Mock<IPlayerVisibilityReader> _visibility = new();

    public PlayerProfileHandlerTests()
    {
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(Caller).Build());
        // No relation by default: the caller sees public players only.
        _visibility.Setup(v => v.GetAudience(Caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(Caller, new Dictionary<Guid, IReadOnlyList<Name>>(), new HashSet<Guid>()));
        _playerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Target, 12000, 22, 500, 0, 0, 900, 950000, 20.5,
                871, 960000, 20.9, 852, 940000, 20.1, 20.6, 20.8, 20.2));
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
    }

    private PlayerProfileHandler Build() => new(_users.Object, _visibility.Object, _currentUser.Object,
        _playerStats.Object, _charts.Object, _scores.Object);

    private void TargetIs(bool isPublic) =>
        _users.Setup(u => u.GetUser(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(Target).WithName("Target").WithIsPublic(isPublic).Build());

    private void CallerShares(string community) =>
        _visibility.Setup(v => v.GetAudience(Caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(Caller,
                new Dictionary<Guid, IReadOnlyList<Name>> { [Target] = new[] { Name.From(community) } },
                new HashSet<Guid>()));

    [Fact]
    public async Task APrivateStrangerReadsAsNothing()
    {
        TargetIs(isPublic: false);

        var profile = await Build().Handle(new GetPlayerProfileQuery(Target, MixEnum.Phoenix), CancellationToken.None);

        Assert.Null(profile);
        _playerStats.Verify(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task APrivatePlayerYouShareACommunityWithReadsAndNamesTheCommunity()
    {
        TargetIs(isPublic: false);
        CallerShares("Seoul Pump");

        var profile = await Build().Handle(new GetPlayerProfileQuery(Target, MixEnum.Phoenix), CancellationToken.None);

        Assert.NotNull(profile);
        Assert.True(profile!.Visibility.CanView);
        Assert.Equal(new[] { Name.From("Seoul Pump") }, profile.Visibility.SharedCommunities);
    }

    [Fact]
    public async Task AnonymousSeesAPublicPlayer()
    {
        TargetIs(isPublic: true);
        _currentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        _visibility.Setup(v => v.GetAudience(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerAudience.Anonymous);

        var profile = await Build().Handle(new GetPlayerProfileQuery(Target, MixEnum.Phoenix), CancellationToken.None);

        Assert.NotNull(profile);
        Assert.False(profile!.Visibility.IsYou);
    }

    [Fact]
    public async Task ProfileProjectsStatsAndFolderCompletion()
    {
        TargetIs(isPublic: true);
        var passed = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var unpassed = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var coOp = new ChartBuilder().WithLevel(2).WithType(ChartType.CoOp).Build();
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { passed, unpassed, coOp });
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RecordedPhoenixScore(passed.Id, 990000, PhoenixPlate.SuperbGame, false, Now) });

        var profile = await Build().Handle(new GetPlayerProfileQuery(Target, MixEnum.Phoenix), CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(900, profile!.Pumbility);
        Assert.Equal(20.8, profile.SinglesCompetitiveLevel);
        Assert.Equal(20.2, profile.DoublesCompetitiveLevel);
        // S20 and D20 are separate folders. Both level-20 charts are doubles, so D20 holds them
        // both with one pass and there is no S20 folder at all; the co-op chart stays out.
        var doubles = profile.FolderCompletion.Single(f => f.Level == 20 && f.Type == ChartType.Double);
        Assert.Equal(1, doubles.Passed);
        Assert.Equal(2, doubles.Total);
        Assert.Equal(1, doubles.GradeCounts[PhoenixLetterGrade.SSS]);
        Assert.DoesNotContain(profile.FolderCompletion, f => f.Level == 20 && f.Type == ChartType.Single);
        Assert.DoesNotContain(profile.FolderCompletion, f => f.Level == 2);
    }

    [Fact]
    public async Task ALegacyMixReadsPassesFromTheLegacyStore()
    {
        TargetIs(isPublic: true);
        var chart = new ChartBuilder().WithLevel(18).WithType(ChartType.Single).Build();
        _charts.Setup(c => c.GetCharts(MixEnum.XX, It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _playerStats.Setup(p => p.GetStats(MixEnum.XX, Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Target, 0, 18, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        _scores.Setup(s => s.GetBestXXAttempts(MixEnum.XX, Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BestXXChartAttempt(chart, new XXChartAttempt(XXLetterGrade.S, false, null, Now))
            });

        var profile = await Build().Handle(new GetPlayerProfileQuery(Target, MixEnum.XX), CancellationToken.None);

        var singles = profile!.FolderCompletion.Single(f => f.Level == 18 && f.Type == ChartType.Single);
        Assert.Equal(1, singles.Passed);
        Assert.Equal(1, singles.GradeCounts[PhoenixLetterGrade.S]);
        _scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
