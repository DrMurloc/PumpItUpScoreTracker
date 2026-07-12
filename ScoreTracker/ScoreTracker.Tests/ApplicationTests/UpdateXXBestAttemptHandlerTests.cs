using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
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

        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object, clock.Object,
            charts.Object);
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

        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object,
            FakeDateTime.At(2026, 1, 1).Object, charts.Object);
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

        var handler = new UpdateXXBestAttemptHandler(attempts.Object, currentUser.Object,
            FakeDateTime.At(2026, 7, 11).Object, charts.Object);
        await handler.Handle(
            new UpdateXXBestAttemptCommand(chart.Id, XXLetterGrade.A, false, null, MixEnum.Prime),
            CancellationToken.None);

        attempts.Verify(a => a.SetBestAttempt(user.Id,
            It.Is<Chart>(c => c.Mix == MixEnum.Prime),
            It.Is<XXChartAttempt>(x => x.LetterGrade == XXLetterGrade.A),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        charts.Verify(c => c.GetChart(MixEnum.XX, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
