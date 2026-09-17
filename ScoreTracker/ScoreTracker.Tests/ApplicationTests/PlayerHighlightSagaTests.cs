using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerHighlightSagaTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IPlayerHighlightRepository> _highlights = new();
    private readonly Mock<IPlayerStatsReader> _playerStats = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IMediator> _mediator = new();
    private IDictionary<string, string> _settings = new Dictionary<string, string>();

    // The capture logic lives in the capturer now; the saga just delegates + isolates failures.
    private PlayerHighlightCapturer Capturer()
    {
        _playerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0));
        _highlights.Setup(h => h.Add(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _settings);
        return new(_charts.Object, _scores.Object, _highlights.Object, _playerStats.Object,
            new MemoryCache(new MemoryCacheOptions()), _bus.Object, _mediator.Object);
    }

    private void SetupPopulation(Chart chart, int pgHolders, int activePlayers)
    {
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _scores.Setup(s => s.GetChartScoreAggregates(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartScoreAggregate(chart.Id, activePlayers, activePlayers, pgHolders) });
        _scores.Setup(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, activePlayers).Select(_ => Guid.NewGuid()).ToHashSet());
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
    public async Task CapturerPersistsANotablePgAndAnnouncesIt()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).WithSongName("Bee").Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);
        var userId = Guid.NewGuid();
        var e = PgEvent(userId, chart.Id);

        await Capturer().Capture(e, CancellationToken.None);

        _highlights.Verify(h => h.Add(e.EventId, userId, MixEnum.Phoenix, When, null,
            It.Is<IReadOnlyList<SignificantWin>>(w => w.Any(x => x.Kind == WinKind.NotablePg && x.ChartId == chart.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
        // The announcement is what every audience acts on, so it rides with the write.
        _bus.Verify(b => b.Publish(It.Is<PlayerHighlightsStoredEvent>(p => p.EventId == e.EventId
            && p.UserId == userId && p.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AHardmodeRungIsNotAWinForAPlayerWithHardmodeOff()
    {
        // Every account starts off (D30), and the feeds follow the switch from the moment it
        // changes: the win is never written, rather than written and hidden on read.
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).Build();
        SetupPopulation(chart, pgHolders: 500, activePlayers: 1000);

        await Capturer().Capture(RungEvent(Guid.NewGuid()), CancellationToken.None);

        _highlights.Verify(h => h.Add(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AHardmodeRungIsAWinForAPlayerWithHardmodeOn()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).Build();
        SetupPopulation(chart, pgHolders: 500, activePlayers: 1000);
        _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [HardmodeOptIn.SettingKey] = "true"
        };
        var userId = Guid.NewGuid();

        await Capturer().Capture(RungEvent(userId), CancellationToken.None);

        _highlights.Verify(h => h.Add(It.IsAny<Guid>(), userId, MixEnum.Phoenix2, It.IsAny<DateTimeOffset>(),
            It.IsAny<Guid?>(),
            It.Is<IReadOnlyList<SignificantWin>>(w => w.Any(x => x.Kind == WinKind.HardmodeTitle
                                                              && x.TitleName == "[P.B] BRONZE")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedSettingsReadDropsOnlyTheHardmodeWins()
    {
        // The switch decides whether Hardmode is announced, not whether the batch is: a transient
        // failure reading it costs the Hardmode rung and keeps the rare PG beside it.
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).WithSongName("Bee").Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);
        var userId = Guid.NewGuid();
        var capturer = Capturer();
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("settings unavailable"));
        var e = ScoreHighlightsCapturedEvent.Create(When, userId, MixEnum.Phoenix2, sessionId: null,
            PgEvent(userId, chart.Id).Changes, RungEvent(userId).Milestones);

        await capturer.Capture(e, CancellationToken.None);

        _highlights.Verify(h => h.Add(e.EventId, userId, MixEnum.Phoenix2, When, null,
            It.Is<IReadOnlyList<SignificantWin>>(w => w.Any(x => x.Kind == WinKind.NotablePg)
                                                      && w.All(x => x.Kind != WinKind.HardmodeTitle)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABatchWithNoHardmodeInItNeverAsksForTheSetting()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).WithSongName("Bee").Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);

        await Capturer().Capture(PgEvent(Guid.NewGuid(), chart.Id), CancellationToken.None);

        _mediator.Verify(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ScoreHighlightsCapturedEvent RungEvent(Guid userId) =>
        ScoreHighlightsCapturedEvent.Create(When, userId, MixEnum.Phoenix2, sessionId: null,
            Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.HardmodePumbilityGain, null, When, 9800, 10200, null,
                    "140|301")
            });

    [Fact]
    public async Task CapturerWritesNothingWhenThereAreNoBigWins()
    {
        var chart = new ChartBuilder().WithLevel(18).WithType(ChartType.Single).Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);

        await Capturer().Capture(PgEvent(Guid.NewGuid(), chart.Id, plate: null), CancellationToken.None);

        _highlights.Verify(h => h.Add(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CapturerWritesNothingForAnEmptyEvent()
    {
        var e = ScoreHighlightsCapturedEvent.Create(When, Guid.NewGuid(), MixEnum.Phoenix, sessionId: null,
            Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>());

        await Capturer().Capture(e, CancellationToken.None);

        _charts.Verify(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
            It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Never);
        _highlights.Verify(h => h.Add(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     A redelivered event finds the row already there. Re-announcing it would have every
    ///     audience re-index the same win.
    /// </summary>
    [Fact]
    public async Task CapturerDoesNotAnnounceAnEventItHadAlreadyStored()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).WithSongName("Bee").Build();
        SetupPopulation(chart, pgHolders: 5, activePlayers: 1000);
        var capturer = Capturer();
        _highlights.Setup(h => h.Add(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<SignificantWin>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await capturer.Capture(PgEvent(Guid.NewGuid(), chart.Id), CancellationToken.None);

        _bus.Verify(b => b.Publish(It.IsAny<PlayerHighlightsStoredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SagaSwallowsCapturerFailuresSoImportsAreNeverDisrupted()
    {
        var capturer = new Mock<IPlayerHighlightCapturer>();
        capturer.Setup(c => c.Capture(It.IsAny<ScoreHighlightsCapturedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var saga = new PlayerHighlightSaga(capturer.Object, NullLogger<PlayerHighlightSaga>.Instance);

        var thrown = await Record.ExceptionAsync(() => saga.Consume(Context(PgEvent(Guid.NewGuid(), Guid.NewGuid()))));

        Assert.Null(thrown);
    }
}
