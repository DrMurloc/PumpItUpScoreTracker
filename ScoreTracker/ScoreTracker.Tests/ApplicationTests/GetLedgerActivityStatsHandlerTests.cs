using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetLedgerActivityStatsHandlerTests
{
    [Fact]
    public async Task ZeroFillsQuietDaysAcrossTheFullWindowOldestFirst()
    {
        var stats = new Mock<ILedgerStatsRepository>();
        stats.Setup(s => s.GetTotals(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerTotals(0, 0));
        // Sparse repo result: activity on only two days of the window.
        stats.Setup(s => s.GetDailyVolumes(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LedgerDayVolume(new DateOnly(2026, 7, 10), 33732),
                new LedgerDayVolume(new DateOnly(2026, 7, 3), 1072)
            });
        var handler = BuildHandler(stats);

        var result = await handler.Handle(new GetLedgerActivityStatsQuery(), CancellationToken.None);

        Assert.Equal(GetLedgerActivityStatsHandler.PulseDays, result.DailyVolumes.Count);
        Assert.Equal(new DateOnly(2026, 6, 13), result.DailyVolumes.First().Day);
        Assert.Equal(new DateOnly(2026, 7, 12), result.DailyVolumes.Last().Day);
        Assert.Equal(33732, result.DailyVolumes.Single(v => v.Day == new DateOnly(2026, 7, 10)).Count);
        Assert.Equal(1072, result.DailyVolumes.Single(v => v.Day == new DateOnly(2026, 7, 3)).Count);
        Assert.Equal(2, result.DailyVolumes.Count(v => v.Count > 0));
        Assert.Equal(result.DailyVolumes.OrderBy(v => v.Day), result.DailyVolumes);
    }

    [Fact]
    public async Task RequestsExactlyThirtyDaysEndingTodayUtc()
    {
        var stats = new Mock<ILedgerStatsRepository>();
        stats.Setup(s => s.GetTotals(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerTotals(0, 0));
        stats.Setup(s => s.GetDailyVolumes(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LedgerDayVolume>());
        var handler = BuildHandler(stats);

        await handler.Handle(new GetLedgerActivityStatsQuery(), CancellationToken.None);

        // 2026-07-12 minus 29 days = 2026-06-13 midnight UTC: today is the 30th bucket.
        stats.Verify(s => s.GetDailyVolumes(
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SumsBothScoreModelsIntoTheHeadlineTotal()
    {
        var stats = new Mock<ILedgerStatsRepository>();
        stats.Setup(s => s.GetTotals(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerTotals(996233, 17274));
        stats.Setup(s => s.GetDailyVolumes(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LedgerDayVolume>());
        var handler = BuildHandler(stats);

        var result = await handler.Handle(new GetLedgerActivityStatsQuery(), CancellationToken.None);

        Assert.Equal(996233, result.PhoenixRecordCount);
        Assert.Equal(17274, result.LegacyAttemptCount);
        Assert.Equal(1013507, result.TotalRecords);
    }

    private static GetLedgerActivityStatsHandler BuildHandler(Mock<ILedgerStatsRepository> stats)
    {
        // Late-evening UTC timestamp: the UTC calendar date is what buckets the window.
        var clock = FakeDateTime.At(2026, 7, 12, 22, 30, 0);
        return new GetLedgerActivityStatsHandler(stats.Object, clock.Object);
    }
}
