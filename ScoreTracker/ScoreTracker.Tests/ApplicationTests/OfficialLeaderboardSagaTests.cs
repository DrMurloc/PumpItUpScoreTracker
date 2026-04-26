using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class OfficialLeaderboardSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConsumeStartLeaderboardImportFansOutToPopularityLeaderboardsAndWorldRankings()
    {
        var mediator = new Mock<IMediator>();
        var worldRankings = new Mock<IWorldRankingService>();
        var saga = BuildSaga(mediator: mediator, worldRankings: worldRankings);

        await saga.Consume(BuildContext(new StartLeaderboardImportEvent()));

        mediator.Verify(m => m.Send(It.IsAny<ProcessChartPopularityCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<ProcessOfficialLeaderboardsCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
        worldRankings.Verify(w => w.CalculateWorldRankings(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleGetGameCardsQueryDelegatesToOfficialSiteClient()
    {
        var officialSite = new Mock<IOfficialSiteClient>();
        var expected = new[] { new GameCardRecord(Name.From("alice"), Id: "card1", IsActive: true) };
        officialSite.Setup(s => s.GetGameCards("user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var saga = BuildSaga(officialSite: officialSite);

        var result = await saga.Handle(new GetGameCardsQuery("user", "pass"), CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task HandleProcessOfficialLeaderboardsClearsEachLeaderboardBeforeWritingEntries()
    {
        var officialSite = new Mock<IOfficialSiteClient>();
        officialSite.Setup(s => s.GetLeaderboardEntries(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new UserOfficialLeaderboard("alice", Place: 0, "Rating", "Top Singles", Score: 100),
            new UserOfficialLeaderboard("bob", Place: 0, "Rating", "Top Singles", Score: 90)
        });
        officialSite.Setup(s => s.GetAllOfficialChartScores(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OfficialChartLeaderboardEntry>());
        var leaderboards = new Mock<IOfficialLeaderboardRepository>();
        var operations = new List<string>();
        leaderboards.Setup(l => l.ClearLeaderboard(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, name, _) => operations.Add($"clear:{name}"))
            .Returns(Task.CompletedTask);
        leaderboards.Setup(l => l.WriteEntries(It.IsAny<IEnumerable<UserOfficialLeaderboard>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<UserOfficialLeaderboard>, CancellationToken>((batch, _) =>
                operations.Add($"write:{batch.First().LeaderboardName}"))
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(officialSite: officialSite, leaderboards: leaderboards);

        await saga.Handle(new ProcessOfficialLeaderboardsCommand(), CancellationToken.None);

        Assert.Equal(new[] { "clear:Top Singles", "write:Top Singles" },
            operations.Where(o => o.EndsWith("Top Singles")).ToArray());
    }

    [Fact]
    public async Task HandleProcessOfficialLeaderboardsAssignsTiedScoresTheSamePlaceWithOlympicGap()
    {
        var officialSite = new Mock<IOfficialSiteClient>();
        // Two players tied at 100, one at 50 → tied players share place 1, next is place 3 (skipping 2).
        officialSite.Setup(s => s.GetLeaderboardEntries(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new UserOfficialLeaderboard("alice", 0, "Rating", "Top", 100),
            new UserOfficialLeaderboard("bob", 0, "Rating", "Top", 100),
            new UserOfficialLeaderboard("carol", 0, "Rating", "Top", 50)
        });
        officialSite.Setup(s => s.GetAllOfficialChartScores(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OfficialChartLeaderboardEntry>());
        var leaderboards = new Mock<IOfficialLeaderboardRepository>();
        IEnumerable<UserOfficialLeaderboard>? captured = null;
        leaderboards.Setup(l => l.WriteEntries(It.IsAny<IEnumerable<UserOfficialLeaderboard>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<UserOfficialLeaderboard>, CancellationToken>((batch, _) =>
                captured = batch.ToArray());
        var saga = BuildSaga(officialSite: officialSite, leaderboards: leaderboards);

        await saga.Handle(new ProcessOfficialLeaderboardsCommand(), CancellationToken.None);

        Assert.NotNull(captured);
        var byUser = captured!.ToDictionary(e => e.Username);
        Assert.Equal(1, byUser["alice"].Place);
        Assert.Equal(1, byUser["bob"].Place);
        Assert.Equal(3, byUser["carol"].Place);
    }

    [Fact]
    public async Task HandleProcessOfficialLeaderboardsSavesAvatarOncePerUniqueUsername()
    {
        var aliceAvatar = new Uri("https://example.invalid/alice.png");
        var bobAvatar = new Uri("https://example.invalid/bob.png");
        var chart1 = new ChartBuilder().Build();
        var chart2 = new ChartBuilder().Build();
        var officialSite = new Mock<IOfficialSiteClient>();
        officialSite.Setup(s => s.GetLeaderboardEntries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserOfficialLeaderboard>());
        // Alice appears on two charts; Bob appears on one.
        officialSite.Setup(s => s.GetAllOfficialChartScores(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new OfficialChartLeaderboardEntry("alice", chart1, 950000, aliceAvatar),
            new OfficialChartLeaderboardEntry("alice", chart2, 920000, aliceAvatar),
            new OfficialChartLeaderboardEntry("bob", chart1, 900000, bobAvatar)
        });
        var leaderboards = new Mock<IOfficialLeaderboardRepository>();
        var saga = BuildSaga(officialSite: officialSite, leaderboards: leaderboards);

        await saga.Handle(new ProcessOfficialLeaderboardsCommand(), CancellationToken.None);

        leaderboards.Verify(l => l.SaveAvatar("alice", aliceAvatar, It.IsAny<CancellationToken>()), Times.Once);
        leaderboards.Verify(l => l.SaveAvatar("bob", bobAvatar, It.IsAny<CancellationToken>()), Times.Once);
        leaderboards.Verify(l => l.SaveAvatar(It.IsAny<string>(), It.IsAny<Uri>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleProcessChartPopularitySavesEntriesUnderPopularityTierList()
    {
        var chart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var officialSite = new Mock<IOfficialSiteClient>();
        officialSite.Setup(s => s.GetOfficialChartLeaderboardEntries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartPopularityLeaderboardEntry(chart, Place: 100, new Uri("https://example.invalid/img.png"))
            });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(officialSite: officialSite, tierLists: tierLists);

        await saga.Handle(new ProcessChartPopularityCommand(), CancellationToken.None);

        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => (string)e.TierListName == "Popularity" && e.ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleProcessChartPopularityClassifiesPlaceMinusOneAsUnrecorded()
    {
        var chart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var officialSite = new Mock<IOfficialSiteClient>();
        officialSite.Setup(s => s.GetOfficialChartLeaderboardEntries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartPopularityLeaderboardEntry(chart, Place: -1, new Uri("https://example.invalid/img.png"))
            });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(officialSite: officialSite, tierLists: tierLists);

        await saga.Handle(new ProcessChartPopularityCommand(), CancellationToken.None);

        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => e.Category == TierListCategory.Unrecorded && e.ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OfficialLeaderboardSaga BuildSaga(
        Mock<IOfficialSiteClient>? officialSite = null,
        Mock<ITierListRepository>? tierLists = null,
        Mock<IWorldRankingService>? worldRankings = null,
        Mock<IOfficialLeaderboardRepository>? leaderboards = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IUserRepository>? user = null,
        Mock<IMediator>? mediator = null,
        Mock<IPiuTrackerClient>? piuTracker = null,
        Mock<IBus>? bus = null,
        Mock<IFileUploadClient>? files = null,
        Mock<IChartRepository>? charts = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        officialSite ??= new Mock<IOfficialSiteClient>();
        tierLists ??= new Mock<ITierListRepository>();
        worldRankings ??= new Mock<IWorldRankingService>();
        leaderboards ??= new Mock<IOfficialLeaderboardRepository>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        user ??= new Mock<IUserRepository>();
        mediator ??= new Mock<IMediator>();
        piuTracker ??= new Mock<IPiuTrackerClient>();
        bus ??= new Mock<IBus>();
        files ??= new Mock<IFileUploadClient>();
        charts ??= new Mock<IChartRepository>();
        dateTime ??= FakeDateTime.At(Now);
        return new OfficialLeaderboardSaga(officialSite.Object, tierLists.Object, worldRankings.Object,
            leaderboards.Object, currentUser.Object, user.Object, mediator.Object, piuTracker.Object,
            NullLogger<OfficialLeaderboardSaga>.Instance, bus.Object, files.Object, charts.Object,
            dateTime.Object);
    }

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
