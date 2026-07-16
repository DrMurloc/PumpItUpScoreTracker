using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
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
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class OfficialLeaderboardSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetGameCardsQueryReturnsCardsFromOfficialSiteClient()
    {
        var officialSite = new Mock<IOfficialSiteClient>();
        var expected = new[] { new GameCardRecord(Name.From("alice"), Id: "card1", IsActive: true) };
        officialSite.Setup(s => s.SignIn(MixEnum.Phoenix, "user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sid123");
        officialSite.Setup(s => s.GetGameCards(MixEnum.Phoenix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var saga = BuildSaga(officialSite: officialSite);

        var result = await saga.Handle(new GetGameCardsQuery("user", "pass"), CancellationToken.None);

        Assert.Same(expected, result);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // ImportOfficialPlayerScoresCommand characterization (previously untested).
    // This is the existential Phoenix 2 import path — these tests pin current
    // behavior ahead of the rearchitecture; they describe what IS, not what ought.

    private static readonly Guid ImportUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Uri NewAvatar = new("https://example.invalid/new-avatar.png");

    private static ImportOfficialPlayerScoresCommand ImportCommand(bool syncPiuTracker = false,
        MixEnum mix = MixEnum.Phoenix)
    {
        return new ImportOfficialPlayerScoresCommand("user", "pass", "card1", "NEWTAG", false, syncPiuTracker, mix);
    }

    private sealed record ImportFixture(
        Mock<IOfficialSiteClient> Site,
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
        Dictionary<string, string>? uiSettings = null,
        MixEnum mix = MixEnum.Phoenix)
    {
        var existingUser = new User(ImportUserId, Name.From("OldName"), true, Name.From("OLDTAG"),
            new Uri("https://example.invalid/old-avatar.png"), Name.From("Canada"));
        var settings = uiSettings ?? new Dictionary<string, string>();

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.User).Returns(existingUser);

        // User reads and writes go through Identity contracts now, so the mediator
        // stands in where the repository mock used to.
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(mix, "user", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sid123");
        site.Setup(s => s.GetAccountData(mix, It.IsAny<string>(), "card1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameAccountDataImport(NewAvatar, Name.From(accountName),
                new[] { Name.From("Title A"), Name.From("Title B") }, "sid123"));
        site.Setup(s => s.GetScorePageCount(mix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(maxPages);
        site.Setup(s => s.GetRecordedScores(mix, ImportUserId, It.IsAny<string>(), "card1", It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(officialScores ?? Array.Empty<OfficialRecordedScore>());
        site.Setup(s => s.GetGameCards(mix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameCardRecord>());

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScores ?? Array.Empty<RecordedPhoenixScore>());
        mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        return new ImportFixture(site, mediator, currentUser, new Mock<IPiuTrackerClient>(),
            new Mock<IBus>(), settings, existingUser);
    }

    private static OfficialLeaderboardSaga BuildImportSaga(ImportFixture f)
    {
        return BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser,
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
    public async Task ImportStampsOneRunIdAcrossEverySubmission()
    {
        // One import run = one session on the page; the Session Batcher honors this id.
        var chartA = new ChartBuilder().Build();
        var chartB = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[]
            {
                new OfficialRecordedScore(chartA, 920000, PhoenixPlate.FairGame),
                new OfficialRecordedScore(chartB, 930000, PhoenixPlate.FairGame)
            },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        var sessionIds = new List<Guid?>();
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((cmd, _) =>
                sessionIds.Add(((UpdatePhoenixBestAttemptCommand)cmd).SessionId));

        await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.Equal(2, sessionIds.Count);
        Assert.NotNull(sessionIds[0]);
        Assert.Equal(sessionIds[0], sessionIds[1]);
    }

    [Fact]
    public async Task ImportPublishesDetectedTitlesFromAccountData()
    {
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Bus.Verify(b => b.Publish(It.Is<TitlesDetectedEvent>(e =>
                e.UserId == ImportUserId &&
                e.TitlesFound.Contains("Title A") && e.TitlesFound.Contains("Title B") &&
                e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectedTitlesCarryTheRunSessionWhenScoresWereSaved()
    {
        // When the run saved scores, the detected titles ride that session's snapshot card —
        // the event carries the same run id the scores were stamped with.
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        Guid? scoreSession = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((cmd, _) =>
                scoreSession = ((UpdatePhoenixBestAttemptCommand)cmd).SessionId);
        Guid? titleSession = null;
        f.Bus.Setup(b => b.Publish(It.IsAny<TitlesDetectedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TitlesDetectedEvent, CancellationToken>((e, _) => titleSession = e.SessionId)
            .Returns(Task.CompletedTask);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.NotNull(titleSession);
        Assert.Equal(scoreSession, titleSession);
    }

    [Fact]
    public async Task ImportLinksTheGameTagToTheImportingAccount()
    {
        // The import knows the tag authoritatively — it upserts the mirror-player link,
        // and the most recent import wins a contested tag (the repository overwrites).
        var f = ArrangeImport(mix: MixEnum.Phoenix2);
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            piuTracker: f.PiuTracker, bus: f.Bus, identity: identity);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        identity.Verify(i => i.LinkPlayer(MixEnum.Phoenix2, "NEWTAG", ImportUserId,
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedLoginNeverLinksATag()
    {
        var f = ArrangeImport(accountName: "INVALID");
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            piuTracker: f.PiuTracker, bus: f.Bus, identity: identity);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        identity.Verify(i => i.LinkPlayer(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportUpdatesGameTagAndAvatarPreservingOtherProfileFields()
    {
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<UpdateUserGameProfileCommand>(c =>
                c.GameTag.ToString() == "NEWTAG" &&
                c.AvatarUrl == NewAvatar),
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
        f.Mediator.Verify(m => m.Send(It.IsAny<UpdateUserGameProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportRequestsOnlyPagesNewerThanLastImport()
    {
        var f = ArrangeImport(maxPages: 5,
            uiSettings: new Dictionary<string, string> { ["PreviousPageCount"] = "3" });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        // limit = maxPages - previous + 1 = 5 - 3 + 1
        f.Site.Verify(s => s.GetRecordedScores(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(), "card1", false, 3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportStoresPageCountForNextImport()
    {
        var f = ArrangeImport(maxPages: 5);
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName == "PreviousPageCount" && c.NewValue == "5"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportReportsInvalidLoginAsAnErrorAndStops()
    {
        var f = ArrangeImport(accountName: "INVALID");
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e =>
                e.Error == "Invalid Login Information"),
            It.IsAny<CancellationToken>()), Times.Once);
        // A session that can't resolve to an account is terminal — no scrape follows.
        f.Site.Verify(s => s.GetRecordedScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // Phoenix 2 mix threading (commit 9): the backend is fully wired even though
    // the import UI still shows Coming-soon under Phoenix 2 — nothing user-facing
    // dispatches a Phoenix2-mixed command until the owner verifies against his kit.

    [Fact]
    public async Task Phoenix2ImportReadsTheP2SiteAndStampsEverythingPhoenix2()
    {
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(mix: MixEnum.Phoenix2,
            officialScores: new[] { new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame) });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        // Site calls carry the mix — chart resolution and page reads hit the P2 site.
        f.Site.Verify(s => s.GetAccountData(MixEnum.Phoenix2, It.IsAny<string>(), "card1",
            It.IsAny<CancellationToken>()), Times.Once);
        f.Site.Verify(s => s.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, It.IsAny<string>(), "card1",
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);

        // Existing-score comparison reads the Phoenix 2 rows, not Phoenix 1's.
        f.Mediator.Verify(m => m.Send(It.Is<GetPhoenixRecordsQuery>(q =>
                q.UserId == ImportUserId && q.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Once);

        // The persisted best attempt is Phoenix2-mixed (journal + ledger land in P2 rows).
        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                c.ChartId == chart.Id && c.Mix == MixEnum.Phoenix2 &&
                c.Source == ScoreJournalEntry.OfficialImportSource),
            It.IsAny<CancellationToken>()), Times.Once);

        // Status + titles events are Phoenix2-stamped end to end.
        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(e => e.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(e => e.Mix != MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Bus.Verify(b => b.Publish(It.Is<TitlesDetectedEvent>(e =>
                e.UserId == ImportUserId && e.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2ImportKeepsPageCountMemoryPerMix()
    {
        // Both sites paginate independently: the P2 import must read/write its own
        // PreviousPageCount__Phoenix2 key and leave Phoenix 1's legacy key alone.
        var f = ArrangeImport(mix: MixEnum.Phoenix2, maxPages: 5,
            uiSettings: new Dictionary<string, string>
            {
                ["PreviousPageCount"] = "2", // P1 legacy key — must be ignored by a P2 import
                ["PreviousPageCount__Phoenix2"] = "4"
            });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        // limit = maxPages - previous + 1 = 5 - 4 + 1 (from the __Phoenix2 key, not "2").
        f.Site.Verify(s => s.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, It.IsAny<string>(), "card1", false, 2,
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName == "PreviousPageCount__Phoenix2" && c.NewValue == "5"),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName == "PreviousPageCount"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2ImportBackfillsUnclaimedCardAliasesAndNeverRepointsOwnedOnes()
    {
        // Locked decision: /Login/PiuGame stays pinned to Phoenix 1 as the identity source,
        // so P2 card aliases enter through the first P2 import — additively only.
        var f = ArrangeImport(mix: MixEnum.Phoenix2);
        f.Site.Setup(s => s.GetGameCards(MixEnum.Phoenix2, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GameCardRecord(Name.From("NEWTAG"), Id: "7770001", IsActive: true),
                new GameCardRecord(Name.From("ALTTAG"), Id: "7770002", IsActive: false)
            });
        var otherUser = new UserBuilder().Build();
        f.Mediator.Setup(m => m.Send(It.Is<GetUserByExternalLoginQuery>(q =>
                q.ExternalId == "card:7770002" && q.LoginProviderName == "PiuGame"),
            It.IsAny<CancellationToken>())).ReturnsAsync(otherUser);
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<CreateExternalLoginCommand>(c =>
                c.UserId == ImportUserId && c.ExternalId == "card:7770001" && c.LoginProviderName == "PiuGame"),
            It.IsAny<CancellationToken>()), Times.Once);
        // The alias another account already owns is left with its owner — collisions are a
        // merge-invitation concern, never a takeover.
        f.Mediator.Verify(m => m.Send(It.Is<CreateExternalLoginCommand>(c => c.ExternalId == "card:7770002"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixImportDoesNotTouchCardAliases()
    {
        // P1 aliases already backfill on /Login/PiuGame (the identity source); the import
        // path only backfills for Phoenix 2.
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Site.Verify(s => s.GetGameCards(It.IsAny<MixEnum>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateExternalLoginCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static OfficialLeaderboardSaga BuildSaga(
        Mock<IOfficialSiteClient>? officialSite = null,
        Mock<IOfficialPlayerIdentityRepository>? identity = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IMediator>? mediator = null,
        Mock<IPiuTrackerClient>? piuTracker = null,
        Mock<IBus>? bus = null,
        Mock<IFileUploadClient>? files = null,
        Mock<IChartRepository>? charts = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        officialSite ??= new Mock<IOfficialSiteClient>();
        identity ??= new Mock<IOfficialPlayerIdentityRepository>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        mediator ??= new Mock<IMediator>();
        piuTracker ??= new Mock<IPiuTrackerClient>();
        bus ??= new Mock<IBus>();
        files ??= new Mock<IFileUploadClient>();
        charts ??= new Mock<IChartRepository>();
        dateTime ??= FakeDateTime.At(Now);
        return new OfficialLeaderboardSaga(officialSite.Object,
            identity.Object, currentUser.Object, mediator.Object, piuTracker.Object,
            NullLogger<OfficialLeaderboardSaga>.Instance, bus.Object, files.Object, charts.Object,
            dateTime.Object);
    }
}
