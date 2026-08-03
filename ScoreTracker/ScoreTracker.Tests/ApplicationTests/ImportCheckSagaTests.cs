using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The completeness check over mocked ports: what the circuit-side start refuses, that the
///     background body imports before it counts, and that the verdict is published rather than
///     stored — nothing here writes a row, because nothing remembers a check.
/// </summary>
public sealed class ImportCheckSagaTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly Guid OtherChartId = Guid.NewGuid();

    // ---- starting ----

    [Fact]
    public async Task StartingHandsTheScrapeToTheBusAndKeepsThePasswordOnTheCircuit()
    {
        var bus = new Mock<IBus>();

        var result = await Build(bus: bus).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, result.Outcome);
        bus.Verify(b => b.Publish(
            It.Is<RunImportCheckCommand>(c => c.UserId == UserId && !c.DeepScan && c.RepairBuckets.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABadPasswordIsCaughtOnTheCircuitBeforeAnythingIsQueuedOrSpent()
    {
        var bus = new Mock<IBus>();
        var mediator = Mediator();
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCredentialException());

        var result = await Build(bus: bus, mediator: mediator, site: site)
            .Handle(Start(deepScan: true), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.InvalidCredentials, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunImportCheckCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        // A mistyped password must not cost one of the month's three scans.
        mediator.Verify(m => m.Send(It.IsAny<SpendDeepScanCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASecondCheckIsRefusedWhileOneIsInFlight()
    {
        var result = await Build(guard: Guard(userSlot: false)).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.AlreadyRunning, result.Outcome);
    }

    [Fact]
    public async Task APreFlightFailureHandsTheUsersSlotBack()
    {
        var guard = Guard();
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCredentialException());

        await Build(guard: guard, site: site).Handle(Start(), CancellationToken.None);

        // Otherwise a mistyped password locks the player out of retrying until the process restarts.
        guard.Verify(g => g.End(UserId), Times.Once);
    }

    [Fact]
    public async Task ADeepScanSpendsOneOfTheMonthsAllowanceAndAPlainCheckSpendsNothing()
    {
        var mediator = Mediator();
        var saga = Build(mediator: mediator);

        var deep = await saga.Handle(Start(deepScan: true), CancellationToken.None);
        await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, deep.Outcome);
        Assert.Equal(2, deep.DeepScansLeft);
        mediator.Verify(m => m.Send(It.Is<SpendDeepScanCommand>(c => c.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEmptyAllowanceRefusesTheDeepScanButNotTheCensus()
    {
        var mediator = Mediator(scansLeft: 0);
        var saga = Build(mediator: mediator);

        var deep = await saga.Handle(Start(deepScan: true), CancellationToken.None);
        var census = await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.NoDeepScansLeft, deep.Outcome);
        // The allowance rations "walk everything", never the cheap per-level check.
        Assert.Equal(ImportCheckStartOutcome.Started, census.Outcome);
    }

    [Fact]
    public async Task LosingTheAllowanceRaceRefusesRatherThanRunningForFree()
    {
        // The balance read said one was left, but another tab spent it before this one asked.
        var mediator = Mediator(scansLeft: 1, spendGranted: false);

        var result = await Build(mediator: mediator).Handle(Start(deepScan: true), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.NoDeepScansLeft, result.Outcome);
    }

    // ---- running ----

    [Fact]
    public async Task TheCheckImportsBeforeItCounts()
    {
        var mediator = Mediator();
        var order = new List<string>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("import")).Returns(Task.CompletedTask);
        var site = Site(Census(("18", 1)));
        site.Setup(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("census")).ReturnsAsync(Census(("18", 1)));

        await Build(mediator: mediator, site: site, records: Records(1))
            .Handle(Execute(), CancellationToken.None);

        // Counting an account that played twenty minutes ago against scores we have not fetched
        // yet reports charts that are simply not imported yet.
        Assert.Equal(new[] { "import", "census" }, order);
    }

    [Fact]
    public async Task TheVerdictIsPublishedNotStored()
    {
        var mediator = Mediator();

        await Build(mediator: mediator, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(), CancellationToken.None);

        // Nothing persists a check: this notification IS the result, and a page that navigated
        // away simply never receives it.
        mediator.Verify(m => m.Publish(
            It.Is<ImportCheckCompletedEvent>(e => e.UserId == UserId
                                                  && e.Report.Verdict == ImportCheckVerdict.InSync),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AShortLevelIsReadSoTheVerdictCanNameTheChartAndItsScore()
    {
        var mediator = Mediator();
        var site = Site(Census(("18", 2)));
        site.Setup(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OfficialRecordedScore>)new[]
            {
                new OfficialRecordedScore(Chart(ChartId), PhoenixScore.From(990000), PhoenixPlate.MarvelousGame),
                new OfficialRecordedScore(Chart(OtherChartId), PhoenixScore.From(996408), PhoenixPlate.MarvelousGame)
            });

        await Build(mediator: mediator, site: site, records: Records(1)).Handle(Execute(), CancellationToken.None);

        mediator.Verify(m => m.Publish(It.Is<ImportCheckCompletedEvent>(e =>
                e.Report.Repairable.Single().Charts.Single().ChartId == OtherChartId &&
                e.Report.Repairable.Single().Charts.Single().Score == 996408 &&
                // Never imported, so there is no score of ours to show beside it.
                e.Report.Repairable.Single().Charts.Single().CurrentScore == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACleanCensusNeverPaysForANamingWalk()
    {
        var site = Site(Census(("18", 1)));

        await Build(site: site, records: Records(1)).Handle(Execute(), CancellationToken.None);

        site.Verify(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- repairing ----

    [Fact]
    public async Task ARepairReReadsExactlyTheLevelsThePanelAskedFor()
    {
        var mediator = Mediator();

        await Build(mediator: mediator, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(repairBuckets: new[] { "18", "21" }), CancellationToken.None);

        // The panel holds the findings and says what it wants fixed — nothing on the server
        // remembers the last run.
        mediator.Verify(m => m.Send(It.Is<RepairScoresCommand>(c =>
            c.Buckets.SequenceEqual(new[] { "18", "21" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlainCheckRepairsNothing()
    {
        var mediator = Mediator();

        await Build(mediator: mediator, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(), CancellationToken.None);

        mediator.Verify(m => m.Send(It.IsAny<RepairScoresCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeepScanWalksTheWholeListRegardlessOfWhatThePanelAskedFor()
    {
        var mediator = Mediator();

        await Build(mediator: mediator, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(deepScan: true, repairBuckets: new[] { "18" }), CancellationToken.None);

        // No buckets: the only way to catch a score that improved without changing grade or plate.
        mediator.Verify(m => m.Send(It.Is<RepairScoresCommand>(c => c.Buckets.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADeepScanWaitsRatherThanPilingOntoTheOnesAlreadyWalkingTheSite()
    {
        var site = Site(Census(("18", 1)));

        await Build(guard: Guard(deepSlot: false), site: site)
            .Handle(Execute(deepScan: true), CancellationToken.None);

        site.Verify(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheGlobalDeepScanSlotIsAlwaysReturned()
    {
        var guard = Guard();
        var site = Site();
        site.Setup(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("piugame fell over"));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            Build(guard: guard, site: site).Handle(Execute(deepScan: true), CancellationToken.None));

        // A scan that dies mid-walk must not hold a site-wide slot until the process restarts.
        guard.Verify(g => g.EndDeepScan(), Times.Once);
    }

    // ---- builders ----

    private static StartImportCheckCommand Start(bool deepScan = false)
    {
        return new StartImportCheckCommand(new TypedCredentialSource("user", "pass"), MixEnum.Phoenix,
            "card", "TAG #1", deepScan, Array.Empty<string>());
    }

    private static ExecuteImportCheckCommand Execute(bool deepScan = false, string[]? repairBuckets = null)
    {
        return new ExecuteImportCheckCommand(UserId, MixEnum.Phoenix, "sid", "card", "TAG #1", deepScan,
            repairBuckets ?? Array.Empty<string>());
    }

    private static Chart Chart(Guid id)
    {
        return new ChartBuilder().WithId(id).WithType(ChartType.Single).WithLevel(18).Build();
    }

    private static AccountCensus Census(params (string Bucket, int Passes)[] buckets)
    {
        return new AccountCensus(MixEnum.Phoenix,
            buckets.ToDictionary(b => b.Bucket,
                b => new CensusBucket(b.Bucket, b.Passes, new Dictionary<string, int>(),
                    new Dictionary<string, int>()), StringComparer.Ordinal), 64466);
    }

    private static RecordedPhoenixScore[] Records(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new RecordedPhoenixScore(ChartId, PhoenixScore.From(990000), PhoenixPlate.MarvelousGame,
                false, DateTimeOffset.UnixEpoch))
            .ToArray();
    }

    private static Mock<IImportConcurrencyGuard> Guard(bool userSlot = true, bool deepSlot = true)
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        guard.Setup(g => g.TryBegin(It.IsAny<Guid>())).Returns(userSlot);
        guard.Setup(g => g.TryBeginDeepScan()).Returns(deepSlot);
        return guard;
    }

    private static Mock<IOfficialSiteClient> Site(AccountCensus? census = null)
    {
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync("sid");
        site.Setup(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(census ?? Census());
        site.Setup(s => s.GetBestScoresIn(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OfficialRecordedScore>)Array.Empty<OfficialRecordedScore>());
        return site;
    }

    private static Mock<IMediator> Mediator(int scansLeft = 3, bool spendGranted = true)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mediator.Setup(m => m.Send(It.IsAny<RepairScoresCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        mediator.Setup(m => m.Send(It.IsAny<GetDeepScansRemainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scansLeft);
        mediator.Setup(m => m.Send(It.IsAny<SpendDeepScanCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(spendGranted);
        return mediator;
    }

    private static ImportCheckSaga Build(Mock<IBus>? bus = null, Mock<IImportConcurrencyGuard>? guard = null,
        Mock<IMediator>? mediator = null, Mock<IOfficialSiteClient>? site = null,
        RecordedPhoenixScore[]? records = null)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Chart(ChartId), Chart(OtherChartId) });

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records ?? Array.Empty<RecordedPhoenixScore>());

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(u => u.User).Returns(new UserBuilder().WithId(UserId).Build());

        return new ImportCheckSaga(
            (bus ?? new Mock<IBus>()).Object,
            charts.Object,
            currentUser.Object,
            (guard ?? Guard()).Object,
            (mediator ?? Mediator()).Object,
            (site ?? Site()).Object,
            scores.Object);
    }
}
