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
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The completeness check end to end over mocked ports: what the circuit-side start refuses,
///     that the background body imports before it counts, and each verdict the panel renders.
/// </summary>
public sealed class ImportCheckSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();

    // ---- starting ----

    [Fact]
    public async Task StartingHandsTheScrapeToTheBusAndKeepsThePasswordOnTheCircuit()
    {
        var bus = new Mock<IBus>();
        var site = Site();
        var saga = Build(bus: bus, site: site);

        var result = await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, result.Outcome);
        bus.Verify(b => b.Publish(
            It.Is<RunImportCheckCommand>(c => c.UserId == UserId && c.Mix == MixEnum.Phoenix && !c.DeepScan),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABadPasswordIsCaughtOnTheCircuitBeforeAnythingIsQueued()
    {
        var bus = new Mock<IBus>();
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCredentialException());

        var result = await Build(bus: bus, site: site).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.InvalidCredentials, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunImportCheckCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASecondCheckIsRefusedWhileOneIsInFlight()
    {
        var guard = Guard(userSlot: false);

        var result = await Build(guard: guard).Handle(Start(), CancellationToken.None);

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
    public async Task AFourthDeepScanInAMonthIsRefusedButTheCensusIsNot()
    {
        var runs = new Mock<IImportCheckRepository>();
        runs.Setup(r => r.CountDeepScansInMonth(UserId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var saga = Build(runs: runs);

        var deep = await saga.Handle(Start(deepScan: true), CancellationToken.None);
        var census = await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.NoDeepScansLeft, deep.Outcome);
        Assert.Equal(0, deep.DeepScansLeft);
        // The allowance rations "walk everything", never the cheap per-level check.
        Assert.Equal(ImportCheckStartOutcome.Started, census.Outcome);
    }

    // ---- running ----

    [Fact]
    public async Task TheCheckImportsBeforeItCounts()
    {
        var mediator = Mediator();
        var order = new List<string>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("import")).Returns(Task.CompletedTask);
        var site = Site();
        site.Setup(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("census"))
            .ReturnsAsync(Census(("18", 1)));

        await Build(mediator: mediator, site: site).Handle(Execute(), CancellationToken.None);

        // Counting an account that played twenty minutes ago against scores we have not fetched
        // yet reports charts that are simply not imported yet.
        Assert.Equal(new[] { "import", "census" }, order);
    }

    [Fact]
    public async Task AMissingChartIsStoredAsAMissingVerdict()
    {
        var runs = new Mock<IImportCheckRepository>();
        var site = Site(Census(("18", 2)));

        await Build(runs: runs, site: site, records: Records(1)).Handle(Execute(), CancellationToken.None);

        runs.Verify(r => r.Save(It.Is<ImportCheckRun>(run =>
            run.UserId == UserId &&
            run.Kind == ImportCheckKind.Census &&
            run.Findings.Count == 1 &&
            run.Findings.Single().Kind == CensusFindingKind.Missing &&
            run.Findings.Single().Bucket == "18" &&
            run.Findings.Single().Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnAgreeingCensusStoresARunWithNoFindings()
    {
        var runs = new Mock<IImportCheckRepository>();

        await Build(runs: runs, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(), CancellationToken.None);

        runs.Verify(r => r.Save(It.Is<ImportCheckRun>(run => run.Findings.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADeepScanIsRecordedAsOneSoItSpendsTheAllowance()
    {
        var runs = new Mock<IImportCheckRepository>();

        await Build(runs: runs, site: Site(Census(("18", 1))), records: Records(1))
            .Handle(Execute(deepScan: true), CancellationToken.None);

        runs.Verify(r => r.Save(It.Is<ImportCheckRun>(run => run.Kind == ImportCheckKind.Deep),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADeepScanWaitsRatherThanPilingOntoTheOnesAlreadyWalkingTheSite()
    {
        var guard = Guard(deepSlot: false);
        var runs = new Mock<IImportCheckRepository>();
        var site = Site(Census(("18", 1)));

        await Build(guard: guard, runs: runs, site: site).Handle(Execute(deepScan: true), CancellationToken.None);

        site.Verify(s => s.GetOfficialCensus(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        runs.Verify(r => r.Save(It.IsAny<ImportCheckRun>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // ---- reading it back ----

    [Fact]
    public async Task ThePanelReadsTheStoredVerdictAndTheAllowanceWithoutTouchingPiuGame()
    {
        var runs = new Mock<IImportCheckRepository>();
        runs.Setup(r => r.CountDeepScansInMonth(UserId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        runs.Setup(r => r.GetLatest(UserId, MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportCheckRun(Guid.NewGuid(), UserId, MixEnum.Phoenix, Now, ImportCheckKind.Census,
                64466, 63420, 2851, 2848, new[]
                {
                    new CensusFinding("18", CensusFindingKind.Missing, 2),
                    new CensusFinding("coop", CensusFindingKind.Missing, 1)
                }));
        var site = new Mock<IOfficialSiteClient>(MockBehavior.Strict);

        var result = await Build(runs: runs, site: site)
            .Handle(new GetLastImportCheckQuery(UserId, MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(ImportCheckVerdict.MissingScores, result.Report!.Verdict);
        Assert.Equal(3, result.Report.MissingCount);
        Assert.Equal(2, result.DeepScansLeft);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), result.NextScanUnlocksAt);
        // A numeric bucket carries its level; CO-OP is not a level and the panel words it differently.
        Assert.Equal(18, result.Report.Differences.Single(d => d.Bucket == "18").Level);
        Assert.Null(result.Report.Differences.Single(d => d.Bucket == "coop").Level);
        site.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HoldingMoreThanPiuGameReadsAsAheadOfSiteNotAsAProblem()
    {
        var runs = new Mock<IImportCheckRepository>();
        runs.Setup(r => r.GetLatest(UserId, MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportCheckRun(Guid.NewGuid(), UserId, MixEnum.Phoenix, Now, ImportCheckKind.Census,
                64466, 64500, 2851, 2852, new[] { new CensusFinding("sub10", CensusFindingKind.Extra, 1) }));

        var result = await Build(runs: runs)
            .Handle(new GetLastImportCheckQuery(UserId, MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(ImportCheckVerdict.AheadOfSite, result.Report!.Verdict);
        Assert.Equal(0, result.Report.MissingCount);
    }

    [Fact]
    public async Task NeverCheckedReadsAsNoReportWithAFullAllowance()
    {
        var result = await Build().Handle(new GetLastImportCheckQuery(UserId, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Null(result.Report);
        Assert.Equal(3, result.DeepScansLeft);
    }

    // ---- builders ----

    private static StartImportCheckCommand Start(bool deepScan = false)
    {
        return new StartImportCheckCommand(new TypedCredentialSource("user", "pass"), MixEnum.Phoenix,
            "card", "TAG #1", deepScan);
    }

    private static ExecuteImportCheckCommand Execute(bool deepScan = false)
    {
        return new ExecuteImportCheckCommand(UserId, MixEnum.Phoenix, "sid", "card", "TAG #1", deepScan);
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
            .Select(_ => new RecordedPhoenixScore(ChartId, PhoenixScore.From(990000),
                PhoenixPlate.MarvelousGame, false, Now))
            .ToArray();
    }

    /// <summary>Both slots free unless a test says otherwise — the per-user one and the site-wide
    /// deep-scan one are independent refusals with different copy.</summary>
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
        return site;
    }

    private static Mock<IMediator> Mediator()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mediator;
    }

    private static ImportCheckSaga Build(Mock<IBus>? bus = null, Mock<IImportConcurrencyGuard>? guard = null,
        Mock<IMediator>? mediator = null, Mock<IOfficialSiteClient>? site = null,
        Mock<IImportCheckRepository>? runs = null, RecordedPhoenixScore[]? records = null)
    {
        guard ??= Guard();

        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
                { new ChartBuilder().WithId(ChartId).WithType(ChartType.Single).WithLevel(18).Build() });

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records ?? Array.Empty<RecordedPhoenixScore>());

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(u => u.User).Returns(new UserBuilder().WithId(UserId).Build());

        return new ImportCheckSaga(
            (bus ?? new Mock<IBus>()).Object,
            charts.Object,
            currentUser.Object,
            FakeDateTime.At(Now).Object,
            guard.Object,
            (mediator ?? Mediator()).Object,
            (site ?? Site()).Object,
            (runs ?? new Mock<IImportCheckRepository>()).Object,
            scores.Object);
    }
}
