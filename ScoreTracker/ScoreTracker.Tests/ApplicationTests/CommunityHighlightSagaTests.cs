using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunityHighlightSagaTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ICommunityHighlightRepository> _highlights = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<ITitleRepository> _titles = new();

    // The capture logic lives in the capturer now; the saga just delegates + isolates failures.
    private CommunityHighlightCapturer Capturer() => new(_charts.Object, _scores.Object, _titles.Object,
        _highlights.Object, new MemoryCache(new MemoryCacheOptions()));

    private void SetupPopulation(Chart chart, int pgHolders, int activePlayers)
    {
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _scores.Setup(s => s.GetChartScoreAggregates(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartScoreAggregate(chart.Id, activePlayers, activePlayers, pgHolders) });
        _scores.Setup(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, activePlayers).Select(_ => Guid.NewGuid()).ToHashSet());
        _titles.Setup(t => t.GetTitleAggregations(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TitleAggregationRecord>());
        _titles.Setup(t => t.CountTitledUsers(It.IsAny<CancellationToken>())).ReturnsAsync(1000);
    }

    private static ScoreHighlightsCapturedEvent PgEvent(Guid userId, Guid chartId, string? plate = "Perfect Game") =>
        ScoreHighlightsCapturedEvent.Create(When, userId, MixEnum.Phoenix, sessionId: null,
            new[]
            {
                new ScoreHighlightsCapturedEvent.HighlightedChange(chartId, IsNewPass: true, OldScore: null,
                    NewScore: null, plate, IsBroken: false, HighlightFlags.None)
            });

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task CapturerPersistsANotablePgToTheWinnersCommunities()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).WithSongName("Bee").Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);
        var userId = Guid.NewGuid();
        var e = PgEvent(userId, chart.Id);

        await Capturer().Capture(e, CancellationToken.None);

        _highlights.Verify(h => h.AddForUserCommunities(e.EventId, userId, MixEnum.Phoenix, When, null,
            It.Is<IReadOnlyList<SignificantWin>>(w => w.Any(x => x.Kind == WinKind.NotablePg && x.ChartId == chart.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CapturerWritesNothingWhenThereAreNoBigWins()
    {
        var chart = new ChartBuilder().WithLevel(18).WithType(ChartType.Single).Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);

        await Capturer().Capture(PgEvent(Guid.NewGuid(), chart.Id, plate: null), CancellationToken.None);

        _highlights.Verify(h => h.AddForUserCommunities(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SagaSwallowsCapturerFailuresSoImportsAreNeverDisrupted()
    {
        var capturer = new Mock<ICommunityHighlightCapturer>();
        capturer.Setup(c => c.Capture(It.IsAny<ScoreHighlightsCapturedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var saga = new CommunityHighlightSaga(capturer.Object, NullLogger<CommunityHighlightSaga>.Instance);

        var thrown = await Record.ExceptionAsync(() => saga.Consume(Context(PgEvent(Guid.NewGuid(), Guid.NewGuid()))));

        Assert.Null(thrown);
    }
}
