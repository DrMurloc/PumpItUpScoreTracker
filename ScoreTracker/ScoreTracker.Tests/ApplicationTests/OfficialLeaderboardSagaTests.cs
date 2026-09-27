using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
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

    private static ImportOfficialPlayerScoresCommand ImportCommand(MixEnum mix = MixEnum.Phoenix)
    {
        return new ImportOfficialPlayerScoresCommand("user", "pass", "NEWTAG", false, mix);
    }

    private sealed record ImportFixture(
        Mock<IOfficialSiteClient> Site,
        Mock<IMediator> Mediator,
        Mock<ICurrentUserAccessor> CurrentUser,
        Mock<ISessionDeliveryClient> SessionDelivery,
        Mock<IBus> Bus,
        Dictionary<string, string> UiSettings,
        User ExistingUser);

    private static ImportFixture ArrangeImport(
        IEnumerable<OfficialRecordedScore>? officialScores = null,
        IEnumerable<RecordedPhoenixScore>? existingScores = null,
        string accountName = "NEWTAG",
        int maxPages = 5,
        Dictionary<string, string>? uiSettings = null,
        MixEnum mix = MixEnum.Phoenix,
        string cardId = "card1")
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
        site.Setup(s => s.GetAccountData(mix, It.IsAny<string>(), cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiuGameAccountDataImport(NewAvatar, Name.From(accountName),
                new[] { Name.From("Title A"), Name.From("Title B") }, "sid123"));
        site.Setup(s => s.GetScorePageCount(mix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(maxPages);
        site.Setup(s => s.GetRecordedScores(mix, ImportUserId, It.IsAny<string>(), cardId, It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScrapedScores((officialScores ?? Array.Empty<OfficialRecordedScore>()).ToArray(),
                Array.Empty<RecordObservedPlaysCommand.ObservedPlay>()));
        // The card the v1 import picks off the same sign-in: the one tagged NEWTAG.
        site.Setup(s => s.GetGameCards(mix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new GameCardRecord(Name.From("NEWTAG"), Id: cardId, IsActive: true) });

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScores ?? Array.Empty<RecordedPhoenixScore>());
        mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        return new ImportFixture(site, mediator, currentUser, new Mock<ISessionDeliveryClient>(),
            new Mock<IBus>(), settings, existingUser);
    }

    private static OfficialLeaderboardSaga BuildImportSaga(ImportFixture f)
    {
        return BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser,
            mediator: f.Mediator, sessionDelivery: f.SessionDelivery, bus: f.Bus);
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
            .Callback<IRequest<ScoreSaveResult>, CancellationToken>((cmd, _) =>
                sessionIds.Add(((UpdatePhoenixBestAttemptCommand)cmd).SessionId));

        await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.Equal(2, sessionIds.Count);
        Assert.NotNull(sessionIds[0]);
        Assert.Equal(sessionIds[0], sessionIds[1]);
    }

    [Fact]
    public async Task AnImportAnnouncesOnceWithTheTitlesItFound()
    {
        // The account's titles ride the run's one announcement; the Ledger decides whether that is a
        // score event or, when nothing changed, the titles on their own. The import never publishes
        // them itself.
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<AnnounceScoreChangesCommand>(c =>
                c.UserId == ImportUserId && c.Mix == MixEnum.Phoenix &&
                c.TitlesFound!.Contains("Title A") && c.TitlesFound!.Contains("Title B")),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Bus.Verify(b => b.Publish(It.IsAny<TitlesDetectedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheAnnouncementCarriesWhatEverySaveChangedUnderTheRunsSession()
    {
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
        var sentSessions = new List<Guid?>();
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ScoreSaveResult>, CancellationToken>((cmd, _) =>
                sentSessions.Add(((UpdatePhoenixBestAttemptCommand)cmd).SessionId))
            .ReturnsAsync((IRequest<ScoreSaveResult> cmd, CancellationToken _) =>
                new ScoreSaveResult(((UpdatePhoenixBestAttemptCommand)cmd).ChartId, ScoreSaveChange.NewPass));
        AnnounceScoreChangesCommand? announced = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((cmd, _) => announced = (AnnounceScoreChangesCommand)cmd);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.NotNull(announced);
        Assert.Equal(sentSessions[0], announced!.SessionId);
        Assert.Equal(new[] { chartA.Id, chartB.Id }.OrderBy(id => id),
            announced.Saves.Where(r => r.Change == ScoreSaveChange.NewPass).Select(r => r.ChartId).OrderBy(id => id));
    }

    [Fact]
    public async Task EverySaveAnImportMakesIsLeftForTheRunToAnnounce()
    {
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(new ChartBuilder().Build(), 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c => c.DeferAnnouncement),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c => !c.DeferAnnouncement),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheRunAnnouncesBeforeItReportsThatChartsFinishedSaving()
    {
        // "Charts finished saving" is what the page and the nav pulse treat as done; by then the
        // follow-up must already be on its way. It is sent once, by the run, carrying no scores.
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(new ChartBuilder().Build(), 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        var order = new List<string>();
        f.Mediator.Setup(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("announce"));
        f.Mediator.Setup(m => m.Publish(It.IsAny<ImportStatusUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ImportStatusUpdatedEvent, CancellationToken>((e, _) =>
                order.Add(e.Status == "Charts finished saving" ? "finished" : "status"));

        await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.Equal(new[] { "announce", "finished" }, order.Where(o => o != "status"));
    }

    [Fact]
    public async Task AnImportThatFailsAfterSavingAnnouncesWhatItSavedOnceAndStillFails()
    {
        // The saved score is real and a later import will never bring it back (the record already
        // matches), so it is announced before the failure is reported.
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
        f.Mediator.SetupSequence(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScoreSaveResult(chartA.Id, ScoreSaveChange.NewPass))
            .ThrowsAsync(new TimeoutException("sql"));

        await Assert.ThrowsAsync<TimeoutException>(() => saga.Handle(ImportCommand(), CancellationToken.None));

        f.Mediator.Verify(m => m.Send(It.Is<AnnounceScoreChangesCommand>(c =>
                c.Saves.Count == 1 && c.Saves[0].ChartId == chartA.Id && c.TitlesFound!.Contains("Title A")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailureAfterTheAnnouncementNeverAnnouncesASecondTime()
    {
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(new ChartBuilder().Build(), 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        f.Mediator.Setup(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(e => e.Status == "Charts finished saving"),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("hub"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => saga.Handle(ImportCommand(), CancellationToken.None));

        f.Mediator.Verify(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AShutdownMidImportLeavesTheAnnouncementToStartupRecovery()
    {
        // The token is cancelled: the process is going away, and the startup pass replays the whole
        // session from the journal. Announcing here would race the shutdown.
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[]
            {
                new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame),
                new OfficialRecordedScore(new ChartBuilder().Build(), 930000, PhoenixPlate.FairGame)
            },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        using var shutdown = new CancellationTokenSource();
        f.Mediator.SetupSequence(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScoreSaveResult(chart.Id, ScoreSaveChange.NewPass))
            .Returns(() =>
            {
                shutdown.Cancel();
                return Task.FromException<ScoreSaveResult>(new OperationCanceledException(shutdown.Token));
            });

        await Assert.ThrowsAsync<OperationCanceledException>(() => saga.Handle(ImportCommand(), shutdown.Token));

        f.Mediator.Verify(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task APageBookmarkFailureStillAnnouncesWhatTheRunSaved()
    {
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(new ChartBuilder().Build(), 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>());
        var saga = BuildImportSaga(f);
        f.Mediator.Setup(m => m.Send(It.Is<SaveUserUiSettingCommand>(c => c.SettingName == "PreviousPageCount"),
            It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("sql"));

        await Assert.ThrowsAsync<TimeoutException>(() => saga.Handle(ImportCommand(), CancellationToken.None));

        f.Mediator.Verify(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportLinksTheGameTagToTheImportingAccount()
    {
        // The import knows the tag authoritatively — it upserts the mirror-player link,
        // and the most recent import wins a contested tag (the repository overwrites).
        var f = ArrangeImport(mix: MixEnum.Phoenix2);
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, identity: identity);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        identity.Verify(i => i.LinkPlayer(MixEnum.Phoenix2, "NEWTAG", ImportUserId,
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnImportThatNamesNoCardOrTagStillSavesWhatTheAccountHolds()
    {
        // The widget, a saved password and the Score check all send blanks for an account that
        // has never picked a card. The run reads the account as it stands: its scores save and
        // the tag it links is the one the account page reported.
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame) },
            existingScores: Array.Empty<RecordedPhoenixScore>(),
            cardId: "");
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, identity: identity);

        await saga.Handle(new ExecuteImportCommand(ImportUserId, MixEnum.Phoenix, "sid123", "", "", false,
            Guid.NewGuid(), ImportKind.Standard), CancellationToken.None);

        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c => c.ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(i => i.LinkPlayer(MixEnum.Phoenix, "NEWTAG", ImportUserId,
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedLoginNeverLinksATag()
    {
        var f = ArrangeImport(accountName: "INVALID");
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, identity: identity);

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

    // Every import offers the session; who actually receives it is a share, resolved inside the
    // port. There is no per-import flag left to pass.
    [Fact]
    public async Task ImportOffersTheSessionToTheToolsThePlayerGrantedIt()
    {
        var f = ArrangeImport();
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.SessionDelivery.Verify(d => d.DeliverSession(ImportUserId, MixEnum.Phoenix,
            It.IsAny<RedactedString>(), "NEWTAG", It.IsAny<CancellationToken>()), Times.Once);
    }

    // A maker's endpoint being down is the maker's problem. It must not become the player's
    // failed import, and it must not stop the scores landing.
    [Fact]
    public async Task AFailedSessionDeliveryNeitherAbortsTheImportNorSurfacesToThePlayer()
    {
        var f = ArrangeImport();
        f.SessionDelivery.Setup(d => d.DeliverSession(It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                It.IsAny<RedactedString>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("their server is down"));
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Existing-score comparison reads the Phoenix 2 rows, not Phoenix 1's.
        f.Mediator.Verify(m => m.Send(It.Is<GetPhoenixRecordsQuery>(q =>
                q.UserId == ImportUserId && q.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Once);

        // The persisted best attempt is Phoenix2-mixed (journal + ledger land in P2 rows).
        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                c.ChartId == chart.Id && c.Mix == MixEnum.Phoenix2 &&
                c.Source == ScoreJournalEntry.OfficialImportSource),
            It.IsAny<CancellationToken>()), Times.Once);

        // Status and the announcement are Phoenix2-stamped end to end.
        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(e => e.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        f.Mediator.Verify(m => m.Publish(It.Is<ImportStatusUpdatedEvent>(e => e.Mix != MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Send(It.Is<AnnounceScoreChangesCommand>(c =>
                c.UserId == ImportUserId && c.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2ImportIgnoresPageCountsEntirely()
    {
        // The dated (redesigned) best list paginates by its own up-score window inside the
        // site client — passing maxPages null. The P2 import never reads the score page count
        // and never reads or writes the retired page-count keys.
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(mix: MixEnum.Phoenix2,
            uiSettings: new Dictionary<string, string>
            {
                ["PreviousPageCount"] = "2", // legacy keys — never read or written by a dated import
                ["PreviousPageCount__Phoenix2"] = "4"
            },
            officialScores: new[]
            {
                new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame, false,
                    new DateTimeOffset(2026, 7, 17, 23, 11, 30, TimeSpan.FromHours(9))),
                new OfficialRecordedScore(new ChartBuilder().Build(), 930000, PhoenixPlate.FairGame, false,
                    new DateTimeOffset(2026, 7, 17, 23, 16, 30, TimeSpan.FromHours(9)))
            });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(mix: MixEnum.Phoenix2), CancellationToken.None);

        // maxPages null — the classic page-count delta never drives a dated import.
        f.Site.Verify(s => s.GetRecordedScores(MixEnum.Phoenix2, ImportUserId, It.IsAny<string>(), "card1", false,
            null, It.IsAny<CancellationToken>()), Times.Once);
        f.Site.Verify(s => s.GetScorePageCount(MixEnum.Phoenix2, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        f.Mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName.StartsWith("PreviousPageCount")),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2ImportBackfillsUnclaimedCardAliasesAndNeverRepointsOwnedOnes()
    {
        // Locked decision: /Login/PiuGame stays pinned to Phoenix 1 as the identity source,
        // so P2 card aliases enter through the first P2 import — additively only.
        var f = ArrangeImport(mix: MixEnum.Phoenix2, cardId: "7770001");
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

        // Once, for the v1 import's own card choice; a backfill would be a second read.
        f.Site.Verify(s => s.GetGameCards(It.IsAny<MixEnum>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.IsAny<CreateExternalLoginCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // One run, three depths: Import and check and the deep scan are the import with a second pass
    // into the same session and the same announcement (docs/design/import-completeness-check.md).

    private static readonly Guid CensusChartId = Guid.NewGuid();
    private static readonly Guid MissingChartId = Guid.NewGuid();

    private static ExecuteImportCommand Execute(ImportKind kind, Guid? sessionId = null, bool includeBroken = false)
    {
        return new ExecuteImportCommand(ImportUserId, MixEnum.Phoenix, "sid123", "card1", "NEWTAG", includeBroken,
            sessionId ?? Guid.NewGuid(), kind);
    }

    private static Chart LevelEighteen(Guid id)
    {
        return new ChartBuilder().WithId(id).WithType(ChartType.Single).WithLevel(18).Build();
    }

    // The account piugame reports: one level-18 bucket with this many passes.
    private static void ArrangeCensus(ImportFixture f, int passes,
        params OfficialRecordedScore[] reread)
    {
        f.Site.Setup(s => s.GetOfficialCensus(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountCensus(MixEnum.Phoenix,
                new Dictionary<string, CensusBucket>(StringComparer.Ordinal)
                {
                    ["18"] = new("18", passes, new Dictionary<string, int>(), new Dictionary<string, int>())
                }, 64466));
        f.Site.Setup(s => s.GetBestScoresIn(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reread);
    }

    // What we hold: one level-18 pass.
    private static ImportFixture ArrangeCheck()
    {
        return ArrangeImport(existingScores: new[]
        {
            new RecordedPhoenixScore(CensusChartId, PhoenixScore.From(990000), PhoenixPlate.MarvelousGame, false,
                DateTimeOffset.UnixEpoch)
        });
    }

    private static OfficialLeaderboardSaga BuildCheckSaga(ImportFixture f)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { LevelEighteen(CensusChartId), LevelEighteen(MissingChartId) });
        return BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, charts: charts);
    }

    [Fact]
    public async Task ACheckImportsBeforeItCounts()
    {
        // Counting an account that played twenty minutes ago against scores we have not fetched yet
        // reports charts that are simply not imported yet.
        var f = ArrangeCheck();
        ArrangeCensus(f, 1);
        var order = new List<string>();
        f.Site.Setup(s => s.GetRecordedScores(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(), "card1",
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("import"))
            .ReturnsAsync(new ScrapedScores(Array.Empty<OfficialRecordedScore>(),
                Array.Empty<RecordObservedPlaysCommand.ObservedPlay>()));
        f.Site.Setup(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("census"))
            .ReturnsAsync(new AccountCensus(MixEnum.Phoenix, new Dictionary<string, CensusBucket>(), 0));

        await BuildCheckSaga(f).Handle(Execute(ImportKind.Check), CancellationToken.None);

        Assert.Equal(new[] { "import", "census" }, order);
    }

    [Fact]
    public async Task AStandardImportNeverCountsOrReReads()
    {
        var f = ArrangeCheck();
        ArrangeCensus(f, 2);

        await BuildCheckSaga(f).Handle(Execute(ImportKind.Standard), CancellationToken.None);

        f.Site.Verify(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Site.Verify(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Publish(It.IsAny<ImportCheckCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ACleanAccountIsCountedAndNothingMoreIsRead()
    {
        var f = ArrangeCheck();
        ArrangeCensus(f, 1);

        await BuildCheckSaga(f).Handle(Execute(ImportKind.Check), CancellationToken.None);

        // Nothing persists a check: this notification IS the result, and it carries a count because the
        // scores themselves are the answer.
        f.Mediator.Verify(m => m.Publish(
            It.Is<ImportCheckCompletedEvent>(e => e.UserId == ImportUserId && e.Added == 0 && e.Checked == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Site.Verify(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AShortLevelIsReReadAndWhatItYieldsIsSavedIntoTheRunsSession()
    {
        var f = ArrangeCheck();
        ArrangeCensus(f, 2, new OfficialRecordedScore(LevelEighteen(MissingChartId), PhoenixScore.From(996408),
            PhoenixPlate.MarvelousGame));
        var sessionId = Guid.NewGuid();

        var written = await BuildCheckSaga(f).Handle(Execute(ImportKind.Check, sessionId), CancellationToken.None);

        // Nobody is asked to approve their own score from the official site — it just lands, in the
        // same session as the import before it, for the run to announce.
        f.Site.Verify(s => s.GetBestScoresIn(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { "18" })), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                c.ChartId == MissingChartId && c.SessionId == sessionId && c.DeferAnnouncement),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Mediator.Verify(m => m.Publish(It.Is<ImportCheckCompletedEvent>(e => e.Added == 1 && e.Checked == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, written);
    }

    [Fact]
    public async Task ADeepScanWalksEverythingWithoutCountingFirst()
    {
        var f = ArrangeCheck();
        ArrangeCensus(f, 1, new OfficialRecordedScore(LevelEighteen(CensusChartId), PhoenixScore.From(990000),
            PhoenixPlate.MarvelousGame));

        await BuildCheckSaga(f).Handle(Execute(ImportKind.DeepScan), CancellationToken.None);

        // The walk finds everything a count would have pointed at, so the census is wasted work.
        f.Site.Verify(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Site.Verify(s => s.GetBestScoresIn(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(),
            It.Is<IReadOnlyCollection<string>>(b => b.SequenceEqual(new[] { CensusBuckets.All })), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
        // Checked is the bests the walk read; nothing beat what we hold.
        f.Mediator.Verify(m => m.Publish(It.Is<ImportCheckCompletedEvent>(e => e.Added == 0 && e.Checked == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ImportKind.Check, true)]
    [InlineData(ImportKind.Check, false)]
    [InlineData(ImportKind.DeepScan, true)]
    [InlineData(ImportKind.DeepScan, false)]
    public async Task BothPassesReadBrokenBestsExactlyWhenThePlayerChose(ImportKind kind, bool includeBroken)
    {
        // The check told the player their account was complete while walking past every broken best a
        // Phoenix 2 import saves — and the import half once passed a hardcoded true, re-recording every
        // break the Your Data cleanup had just withdrawn. Both reads take the player's choice.
        var f = ArrangeCheck();
        ArrangeCensus(f, 2);

        await BuildCheckSaga(f).Handle(Execute(kind, includeBroken: includeBroken), CancellationToken.None);

        f.Site.Verify(s => s.GetRecordedScores(MixEnum.Phoenix, ImportUserId, It.IsAny<string>(), "card1",
            includeBroken, It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Site.Verify(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(), includeBroken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACheckAnnouncesBothPassesOnceAndFinishesOnce()
    {
        // The import used to announce and report "Charts finished saving" after its own pass, then the
        // repair did both again: two cards for one press, and a page that read the first as the end.
        var chart = new ChartBuilder().Build();
        var f = ArrangeImport(
            officialScores: new[] { new OfficialRecordedScore(chart, 920000, PhoenixPlate.FairGame) },
            existingScores: new[]
            {
                new RecordedPhoenixScore(CensusChartId, PhoenixScore.From(990000), PhoenixPlate.MarvelousGame,
                    false, DateTimeOffset.UnixEpoch)
            });
        ArrangeCensus(f, 2, new OfficialRecordedScore(LevelEighteen(MissingChartId), PhoenixScore.From(996408),
            PhoenixPlate.MarvelousGame));
        f.Mediator.Setup(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<ScoreSaveResult> cmd, CancellationToken _) =>
                new ScoreSaveResult(((UpdatePhoenixBestAttemptCommand)cmd).ChartId, ScoreSaveChange.NewPass));
        var order = new List<string>();
        AnnounceScoreChangesCommand? announced = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<AnnounceScoreChangesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((cmd, _) =>
            {
                announced = (AnnounceScoreChangesCommand)cmd;
                order.Add("announce");
            });
        f.Mediator.Setup(m => m.Publish(It.IsAny<ImportStatusUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ImportStatusUpdatedEvent, CancellationToken>((e, _) =>
            {
                if (e.Status == "Charts finished saving") order.Add("finished");
            });
        f.Mediator.Setup(m => m.Publish(It.IsAny<ImportCheckCompletedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("checked"));

        var written = await BuildCheckSaga(f).Handle(Execute(ImportKind.Check), CancellationToken.None);

        Assert.Equal(new[] { "announce", "finished", "checked" }, order);
        Assert.Equal(new[] { chart.Id, MissingChartId }.OrderBy(id => id),
            announced!.Saves.Select(r => r.ChartId).OrderBy(id => id));
        Assert.Equal(2, written);
    }

    [Fact]
    public async Task AnAccountPiugameCannotResolveIsNeverCounted()
    {
        // The error already told the player; counting a hollow account would report every chart missing.
        var f = ArrangeImport(accountName: "INVALID");
        ArrangeCensus(f, 2);

        await BuildCheckSaga(f).Handle(Execute(ImportKind.Check), CancellationToken.None);

        f.Site.Verify(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Publish(It.IsAny<ImportCheckCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // The v1 import API obeys the Import button's slot and cooldown (owner, 2026-09-27).

    private static Mock<IImportConcurrencyGuard> GuardAnswering(ImportSlot slot)
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        guard.Setup(g => g.TryBegin(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<bool>())).Returns(slot);
        return guard;
    }

    [Theory]
    [InlineData(false, OfficialImportOutcome.AlreadyRunning)]
    [InlineData(true, OfficialImportOutcome.CoolingDown)]
    public async Task AV1ImportRefusedByTheSlotNeverSignsIn(bool coolingDown, OfficialImportOutcome outcome)
    {
        var f = ArrangeImport();
        var guard = GuardAnswering(coolingDown
            ? new ImportSlot(ImportSlotOutcome.CoolingDown, TimeSpan.FromMinutes(3))
            : ImportSlot.AlreadyRunning);
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, guard: guard);

        var result = await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.Equal(outcome, result.Outcome);
        f.Site.Verify(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        guard.Verify(g => g.TryBegin(ImportUserId, MixEnum.Phoenix, Now, true), Times.Once);
        guard.Verify(g => g.End(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AV1ImportStartsTheClockAndHandsTheSlotBack()
    {
        var f = ArrangeImport();
        var guard = GuardAnswering(ImportSlot.Taken);
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, guard: guard);

        var result = await saga.Handle(ImportCommand(), CancellationToken.None);

        Assert.Equal(OfficialImportOutcome.Imported, result.Outcome);
        guard.Verify(g => g.Started(ImportUserId, MixEnum.Phoenix, Now), Times.Once);
        guard.Verify(g => g.End(ImportUserId), Times.Once);
    }

    [Fact]
    public async Task AV1ImportThatFailsStillHandsTheSlotBack()
    {
        var f = ArrangeImport();
        f.Site.Setup(s => s.SignIn(MixEnum.Phoenix, "user", "pass", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("no"));
        var guard = GuardAnswering(ImportSlot.Taken);
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, guard: guard);

        await Assert.ThrowsAsync<InvalidCredentialException>(() => saga.Handle(ImportCommand(),
            CancellationToken.None));

        guard.Verify(g => g.End(ImportUserId), Times.Once);
        // A mistyped password is not an import: the clock never starts.
        guard.Verify(g => g.Started(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    [Fact]
    public async Task AV1ImportPicksTheNamedCardOffItsOwnSignIn()
    {
        var f = ArrangeImport(cardId: "card2");
        f.Site.Setup(s => s.GetGameCards(MixEnum.Phoenix, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GameCardRecord(Name.From("OTHER"), Id: "card1", IsActive: true),
                new GameCardRecord(Name.From("NEWTAG"), Id: "card2", IsActive: false)
            });
        var saga = BuildImportSaga(f);

        await saga.Handle(ImportCommand(), CancellationToken.None);

        f.Site.Verify(s => s.SignIn(MixEnum.Phoenix, "user", "pass", It.IsAny<CancellationToken>()), Times.Once);
        f.Site.Verify(s => s.GetAccountData(MixEnum.Phoenix, It.IsAny<string>(), "card2",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AV1ImportNamingACardTheAccountLacksImportsNothing()
    {
        var f = ArrangeImport();
        var guard = GuardAnswering(ImportSlot.Taken);
        var saga = BuildSaga(officialSite: f.Site, currentUser: f.CurrentUser, mediator: f.Mediator,
            sessionDelivery: f.SessionDelivery, bus: f.Bus, guard: guard);

        var result = await saga.Handle(new ImportOfficialPlayerScoresCommand("user", "pass", "NOBODY", false),
            CancellationToken.None);

        Assert.Equal(OfficialImportOutcome.GameTagNotFound, result.Outcome);
        f.Site.Verify(s => s.GetAccountData(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        guard.Verify(g => g.End(ImportUserId), Times.Once);
    }

    private static OfficialLeaderboardSaga BuildSaga(
        Mock<IOfficialSiteClient>? officialSite = null,
        Mock<IOfficialPlayerIdentityRepository>? identity = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IMediator>? mediator = null,
        Mock<ISessionDeliveryClient>? sessionDelivery = null,
        Mock<IBus>? bus = null,
        Mock<IFileUploadClient>? files = null,
        Mock<IChartRepository>? charts = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null,
        Mock<IImportConcurrencyGuard>? guard = null)
    {
        officialSite ??= new Mock<IOfficialSiteClient>();
        identity ??= new Mock<IOfficialPlayerIdentityRepository>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        mediator ??= new Mock<IMediator>();
        sessionDelivery ??= new Mock<ISessionDeliveryClient>();
        bus ??= new Mock<IBus>();
        files ??= new Mock<IFileUploadClient>();
        charts ??= new Mock<IChartRepository>();
        dateTime ??= FakeDateTime.At(Now);
        return new OfficialLeaderboardSaga(officialSite.Object,
            identity.Object, currentUser.Object, mediator.Object, sessionDelivery.Object,
            NullLogger<OfficialLeaderboardSaga>.Instance, bus.Object, files.Object, charts.Object,
            dateTime.Object, (guard ?? new Mock<IImportConcurrencyGuard>()).Object);
    }
}
