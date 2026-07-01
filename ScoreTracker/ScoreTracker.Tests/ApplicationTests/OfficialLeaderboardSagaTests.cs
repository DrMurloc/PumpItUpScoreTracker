using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Application.Messages;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
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
    public async Task StartLeaderboardImportFansOutToPopularityLeaderboardsAndWorldRankings()
    {
        var mediator = new Mock<IMediator>();
        var worldRankings = new Mock<IWorldRankingService>();
        var saga = BuildSaga(mediator: mediator, worldRankings: worldRankings);

        await saga.Consume(BuildContext(new StartLeaderboardImportCommand()));

        mediator.Verify(m => m.Send(It.IsAny<ProcessChartPopularityCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<ProcessOfficialLeaderboardsCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
        worldRankings.Verify(w => w.CalculateWorldRankings(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGameCardsQueryReturnsCardsFromOfficialSiteClient()
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
    public async Task ProcessOfficialLeaderboardsClearsEachLeaderboardBeforeWritingEntries()
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
    public async Task ProcessOfficialLeaderboardsAssignsTiedScoresTheSamePlaceWithOlympicGap()
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
    public async Task ProcessOfficialLeaderboardsSavesAvatarOncePerUniqueUsername()
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
    public async Task ProcessChartPopularitySavesEntriesUnderPopularityTierList()
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
    public async Task ProcessChartPopularityClassifiesPlaceMinusOneAsUnrecorded()
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

    // ───────────────────────────────────────────────────────────────────────────
    // ImportOfficialPlayerScoresCommand characterization (previously untested).
    // This is the existential Phoenix 2 import path — these tests pin current
    // behavior ahead of the rearchitecture; they describe what IS, not what ought.

    private static readonly Guid ImportUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Uri NewAvatar = new("https://example.invalid/new-avatar.png");

    private static ImportOfficialPlayerScoresCommand ImportCommand(bool syncPiuTracker = false)
    {
        return new ImportOfficialPlayerScoresCommand("user", "pass", "card1", "NEWTAG", false, syncPiuTracker);
    }

    private sealed record ImportFixture(
        Mock<IOfficialSiteClient> Site,
        Mock<IUserRepository> Users,
        Mock<IMediator> Mediator,
        Mock<ICurrentUserAccessor> CurrentUser,
        Mock<IPiuTrackerClient> PiuTracker,
        Mock<IBus> Bus,
        Dictionary<string, string> UiSettings,
        User ExistingUser);

    private static ImportFixture ArrangeImport(
        IEnumerable<OfficialRecordedScore>? officialScores = null,
        IEnumerable<RecordedPhoenixScore>? existingScores = null,
        string accountName = "NEWTAG",
        int maxPages = 5,
        Dictionary<string, string>? uiSettings = null)
    {
        var existingUser = new User(ImportUserId, Name.From("OldName"), true, Name.From("OLDTAG"),
            new Uri("https://example.invalid/old-avatar.png"), Name.From("Canada"));
        var settings = uiSettings ?? new Dictionary<string, string>();

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.User).Returns(existingUser);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(ImportUserId, It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);
        users.Setup(u => u.GetUserUiSettings(ImportUserId, It.IsAny<CancellationToken>())).ReturnsAsync(settings);

        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.GetAccountData("user", "pass", "card1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameAccountDataImport(NewAvatar, Name.From(accountName),
                new[] { Name.From("Title A"), Name.From("Title B") }, "sid123"));
        site.Setup(s => s.GetScorePageCount("user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(maxPages);
        site.Setup(s => s.GetRecordedScores(ImportUserId, "user", "pass", "card1", It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(officialScores ?? Array.Empty<OfficialRecordedScore>());

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScores ?? Array.Empty<RecordedPhoenixScore>());

        return new ImportFixture(site, users, mediator, currentUser, new Mock<IPiuTrackerClient>(),
            new Mock<IBus>(), settings, existingUser);
    }

    private static OfficialLeaderboardSaga BuildImportSaga(ImportFixture f)
    {
        return BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, user: f.Users,
            mediator: f.Mediator, piuTracker: f.PiuTracker, bus: f.Bus);
    }

    [Fact]
    public async Task ImportSavesOnlyNewOrImprovedScores()
    {
        var chartSame = new ChartBuilder().Build();
        var chartNew = new ChartBuilder().Build();
        var chartUnbroke = new ChartBuilder().Build();
        var chartWorse = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[]
            {
                new OfficialRecordedScore(chartSame, 900000, PhoenixPlate.TalentedGame),
                new OfficialRecordedScore(chartNew, 920000, PhoenixPlate.FairGame),
                new OfficialRecordedScore(chartUnbroke, 850000, PhoenixPlate.RoughGame),
                new OfficialRecordedScore(chartWorse, 900000, PhoenixPlate.RoughGame)
            },
            existingScores: new[]
            {
                new RecordedPhoenixScore(chartSame.Id, 900000, PhoenixPlate.TalentedGame, false, Now),
                new RecordedPhoenixScore(chartUnbroke.Id, null, null, true, Now),
                new RecordedPhoenixScore(chartWorse.Id, 950000, PhoenixPlate.SuperbGame, false, Now)
            });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c => c.ChartId == chartNew.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c => c.ChartId == chartUnbroke.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ImportPublishesDetectedTitlesFromAccountData()
    {
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Bus.Verify(b => b.Publish(It.Is<TitlesDetectedEvent>(e =>
                e.UserId == ImportUserId &&
                e.TitlesFound.Contains("Title A") && e.TitlesFound.Contains("Title B")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportUpdatesGameTagAndAvatarPreservingOtherProfileFields()
    {
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Users.Verify(u => u.SaveUser(It.Is<User>(saved =>
                saved.Id == ImportUserId &&
                saved.GameTag.ToString() == "NEWTAG" &&
                saved.ProfileImage == NewAvatar &&
                saved.Name == f.ExistingUser.Name &&
                saved.IsPublic == f.ExistingUser.IsPublic &&
                saved.Country == f.ExistingUser.Country),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportContinuesAfterPiuTrackerRateLimitAndReportsError()
    {
        var f = ArrangeImport();
        f.PiuTracker.Setup(p => p.SyncData(It.IsAny<Name>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PiuTrackerUsedTooRecentException());
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(syncPiuTracker: true), CancellationToken.None);

        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e => e.UserId == ImportUserId),
            It.IsAny<CancellationToken>()), Times.Once);
        // The sync failure does not abort the import — profile save still happens.
        f.Users.Verify(u => u.SaveUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportRequestsOnlyPagesNewerThanLastImport()
    {
        var f = ArrangeImport(maxPages: 5,
            uiSettings: new Dictionary<string, string> { ["PreviousPageCount"] = "3" });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        // limit = maxPages - previous + 1 = 5 - 3 + 1
        f.Site.Verify(s => s.GetRecordedScores(ImportUserId, "user", "pass", "card1", false, 3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportStoresPageCountForNextImport()
    {
        var f = ArrangeImport(maxPages: 5);
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Users.Verify(u => u.SaveUserUiSettings(ImportUserId,
            It.Is<IDictionary<string, string>>(d => d["PreviousPageCount"] == "5"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportReportsInvalidLoginStatus()
    {
        var f = ArrangeImport(accountName: "INVALID");
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(s =>
                s.Status == "Invalid Login Information"),
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
