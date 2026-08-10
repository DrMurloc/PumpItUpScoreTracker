using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UpdateXXBestAttemptHandlerTests
{
    [Fact]
    public async Task PersistsAttemptStampedWithCurrentTime()
    {
        var user = new UserBuilder().Build();
        var chart = new ChartBuilder().Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.XX, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var attempts = new Mock<IXXChartAttemptRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);
        var now = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = FakeDateTime.At(now);

        var journal = new Mock<IScoreJournalRepository>();
        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object, clock.Object,
            charts.Object, journal.Object);
        await handler.Handle(
            new UpdateXXBestAttemptCommand(chart.Id, XXLetterGrade.S, false, 100_000_000),
            CancellationToken.None);

        attempts.Verify(a => a.SetBestAttempt(user.Id, chart,
            It.Is<XXChartAttempt>(x => x.RecordedOn == now && x.LetterGrade == XXLetterGrade.S),
            now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovesAttemptWhenLetterGradeIsNull()
    {
        var user = new UserBuilder().Build();
        var chart = new ChartBuilder().Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.XX, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var attempts = new Mock<IXXChartAttemptRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var journal = new Mock<IScoreJournalRepository>();
        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object,
            FakeDateTime.At(2026, 1, 1).Object, charts.Object, journal.Object);
        await handler.Handle(
            new UpdateXXBestAttemptCommand(chart.Id, null, false, null),
            CancellationToken.None);

        attempts.Verify(a => a.RemoveBestAttempt(user.Id, chart, It.IsAny<CancellationToken>()), Times.Once);
        attempts.Verify(a => a.SetBestAttempt(It.IsAny<Guid>(), It.IsAny<Chart>(), It.IsAny<XXChartAttempt>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MaterializesTheChartForTheRequestedLegacyMix()
    {
        // Recording on a legacy mix keys the attempt to that mix's chart context —
        // the repository derives MixId from Chart.Mix (docs/design/legacy-mixes.md).
        var user = new UserBuilder().Build();
        var chart = new ChartBuilder().WithMix(MixEnum.Prime).WithLevel(18).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Prime, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var attempts = new Mock<IXXChartAttemptRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var journal = new Mock<IScoreJournalRepository>();
        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object,
            FakeDateTime.At(2026, 7, 11).Object, charts.Object, journal.Object);
        await handler.Handle(
            new UpdateXXBestAttemptCommand(chart.Id, XXLetterGrade.A, false, null, MixEnum.Prime),
            CancellationToken.None);

        attempts.Verify(a => a.SetBestAttempt(user.Id,
            It.Is<Chart>(c => c.Mix == MixEnum.Prime),
            It.Is<XXChartAttempt>(x => x.LetterGrade == XXLetterGrade.A),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        charts.Verify(c => c.GetChart(MixEnum.XX, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Legacy records have a history now. The number and the letter ride the journal's
    ///     legacy fields, never the Phoenix ones: an era score is not a PhoenixScore, and 76%
    ///     of the scored legacy records in production are above its 1,000,000 ceiling.
    /// </summary>
    [Fact]
    public async Task RecordingJournalsTheLegacyAxes()
    {
        var ctx = new HandlerContext(MixEnum.Prime2);

        await ctx.Handler.Handle(
            new UpdateXXBestAttemptCommand(ctx.Chart.Id, XXLetterGrade.S, false, 45_282_000, MixEnum.Prime2),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.Append(It.Is<ScoreJournalEntry>(e =>
                e.Mix == MixEnum.Prime2
                && e.Score == null && e.Plate == null
                && e.LegacyGrade == XXLetterGrade.S
                && e.LegacyScore != null && (int)e.LegacyScore.Value == 45_282_000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     An acquisition source raises the axes independently: this run beat the stored score
    ///     but not the stored grade, so the score moves and the grade stays.
    /// </summary>
    [Fact]
    public async Task KeepBestStatsRaisesEachAxisOnItsOwn()
    {
        var ctx = new HandlerContext(MixEnum.Prime2);
        ctx.GivenStored(XXLetterGrade.SS, 900_000);

        await ctx.Handler.Handle(
            new UpdateXXBestAttemptCommand(ctx.Chart.Id, XXLetterGrade.A, false, 950_000, MixEnum.Prime2,
                KeepBestStats: true),
            CancellationToken.None);

        ctx.Attempts.Verify(a => a.SetBestAttempt(ctx.User.Id, ctx.Chart,
            It.Is<XXChartAttempt>(x => x.LetterGrade == XXLetterGrade.SS
                                       && x.Score != null && (int)x.Score.Value == 950_000),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A submission that moves neither axis touches nothing — imports re-see plays constantly.</summary>
    [Fact]
    public async Task KeepBestStatsIgnoresASubmissionThatImprovesNothing()
    {
        var ctx = new HandlerContext(MixEnum.Prime2);
        ctx.GivenStored(XXLetterGrade.SS, 900_000);

        await ctx.Handler.Handle(
            new UpdateXXBestAttemptCommand(ctx.Chart.Id, XXLetterGrade.A, false, 800_000, MixEnum.Prime2,
                KeepBestStats: true),
            CancellationToken.None);

        ctx.Attempts.Verify(a => a.SetBestAttempt(It.IsAny<Guid>(), It.IsAny<Chart>(),
            It.IsAny<XXChartAttempt>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.Journal.Verify(j => j.Append(It.IsAny<ScoreJournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     The manual route overwrites, because a player fixing a typo has to be able to lower
    ///     their own record. Without KeepBestStats the stored best is never even read.
    /// </summary>
    [Fact]
    public async Task TheManualRouteOverwritesAndCanLowerARecord()
    {
        var ctx = new HandlerContext(MixEnum.Prime2);
        ctx.GivenStored(XXLetterGrade.SSS, 900_000);

        await ctx.Handler.Handle(
            new UpdateXXBestAttemptCommand(ctx.Chart.Id, XXLetterGrade.C, false, 100, MixEnum.Prime2),
            CancellationToken.None);

        ctx.Attempts.Verify(a => a.SetBestAttempt(ctx.User.Id, ctx.Chart,
            It.Is<XXChartAttempt>(x => x.LetterGrade == XXLetterGrade.C
                                       && x.Score != null && (int)x.Score.Value == 100),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Attempts.Verify(a => a.GetBestAttempt(It.IsAny<Guid>(), It.IsAny<Chart>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class HandlerContext
    {
        public HandlerContext(MixEnum mix)
        {
            User = new UserBuilder().Build();
            Chart = new ChartBuilder().WithMix(mix).Build();
            Charts.Setup(c => c.GetChart(mix, Chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Chart);
            CurrentUser.SetupGet(c => c.User).Returns(User);
            Handler = new UpdateXXBestAttemptHandler(Attempts.Object, CurrentUser.Object,
                FakeDateTime.At(Now).Object, Charts.Object, Journal.Object);
        }

        public static DateTimeOffset Now => new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        public User User { get; }
        public Chart Chart { get; }
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IXXChartAttemptRepository> Attempts { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public UpdateXXBestAttemptHandler Handler { get; }

        public void GivenStored(XXLetterGrade grade, int? score, bool isBroken = false)
        {
            Attempts.Setup(a => a.GetBestAttempt(User.Id, Chart, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new XXChartAttempt(grade, isBroken,
                    score == null ? null : (XXScore?)score.Value, Now - TimeSpan.FromDays(1)));
        }
    }
}
