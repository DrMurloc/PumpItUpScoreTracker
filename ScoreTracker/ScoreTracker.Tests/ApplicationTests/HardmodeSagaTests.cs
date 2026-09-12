using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The site half of Hardmode (docs/design/hardmode-leaderboard.md §3): the weekly reprice,
///     and the viewer's own page read.
/// </summary>
public sealed class HardmodeSagaTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ZeroesTheMixBeforeWritingSoALostScoreCannotLeaveAStaleTotal()
    {
        var chart = Qualifying(21, ChartType.Single);
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(new[] { chart }, new[] { (Guid.NewGuid(), chart, 970_000) }, ratings);

        await saga.Consume(Rebuilt());

        // Clear first, then write: a chart that stopped qualifying, or a player who lost a score,
        // would otherwise keep last week's number on a board nobody recomputes.
        ratings.Verify(r => r.Clear(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
        ratings.Verify(r => r.Save(MixEnum.Phoenix2,
            It.Is<IReadOnlyCollection<HardmodeRatingRow>>(rows => rows.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PricesOnlyQualifyingChartsIntoTheThreePools()
    {
        var single = Qualifying(21, ChartType.Single);
        var dbl = Qualifying(21, ChartType.Double);
        var offList = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).WithMix(MixEnum.Phoenix2).Build();
        var user = Guid.NewGuid();
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(new[] { single, dbl, offList },
            new[] { (user, single, 970_000), (user, dbl, 960_000), (user, offList, 1_000_000) },
            ratings, qualifying: new[] { single, dbl });

        await saga.Consume(Rebuilt());

        ratings.Verify(r => r.Save(MixEnum.Phoenix2, It.Is<IReadOnlyCollection<HardmodeRatingRow>>(rows =>
                rows.Count == 1
                // The perfect game on the chart that does not qualify contributes nothing,
                // which is the whole point of a second chart set.
                && rows.Single().Held == 2
                && rows.Single().Singles > 0
                && rows.Single().Doubles > 0
                && rows.Single().Combined > rows.Single().Singles),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEmptyListWritesNothing()
    {
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(Array.Empty<Chart>(), Array.Empty<(Guid, Chart, int)>(), ratings,
            qualifying: Array.Empty<Chart>());

        await saga.Consume(Rebuilt());

        ratings.Verify(r => r.Clear(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
        ratings.Verify(r => r.Save(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeRatingRow>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ThePageReadsThePoolFromTheViewersOwnRecords()
    {
        var charts = Enumerable.Range(0, 3).Select(_ => Qualifying(21, ChartType.Single)).ToArray();
        var user = Guid.NewGuid();
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(charts, Array.Empty<(Guid, Chart, int)>(), ratings, qualifying: charts,
            bests: charts.Select((c, i) => (c, 990_000 - i * 10_000)).ToArray());

        var page = await saga.Handle(new GetHardmodePageQuery(user, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(3, page.Pool.Count);
        Assert.Equal(3, page.Combined.Held);
        // Descending value, so the best score holds slot one.
        Assert.Equal(1, page.Pool[0].Place);
        Assert.True(page.Pool[0].Value > page.Pool[1].Value);
        Assert.Equal(3, page.QualifyingCharts);
    }

    [Fact]
    public async Task ThePageRailsAskAPerChartValueAndReportThePoolsOwnAverage()
    {
        var charts = Enumerable.Range(0, 4).Select(_ => Qualifying(24, ChartType.Single)).ToArray();
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(charts, Array.Empty<(Guid, Chart, int)>(), ratings, qualifying: charts,
            bests: charts.Select(c => (c, 995_000)).ToArray());

        var page = await saga.Handle(new GetHardmodePageQuery(Guid.NewGuid(), MixEnum.Phoenix2),
            CancellationToken.None);

        var total = page.Rails.Single(r => r.Pool == ScoreTracker.Domain.Models.Titles.Phoenix2.PumbilityPool.Total);
        // Four charts, so the average is over four — not over fifty. A four-chart pool averaging
        // 400 reads 400; dividing by fifty would understate it by an order of magnitude.
        Assert.Equal(page.Combined.Total / 4, total.Average, 3);
        // The ask is the next rung over fifty, because a pool IS fifty.
        Assert.Equal(total.NextThreshold!.Value / 50d, total.Ask, 3);
        Assert.Null(total.Held); // nothing earned yet on a four-chart Hardmode pool
    }

    [Fact]
    public async Task ThePageIsEmptyBeforeTheCensusHasRun()
    {
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(Array.Empty<Chart>(), Array.Empty<(Guid, Chart, int)>(), ratings,
            qualifying: Array.Empty<Chart>());

        var page = await saga.Handle(new GetHardmodePageQuery(Guid.NewGuid(), MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Empty(page.Pool);
        Assert.Empty(page.Rails);
        Assert.Equal(0, page.QualifyingCharts);
        Assert.Equal(0, page.Combined.Total);
    }

    private static Chart Qualifying(int level, ChartType type)
    {
        return new ChartBuilder().WithType(type).WithLevel(level).WithMix(MixEnum.Phoenix2).Build();
    }

    private static ConsumeContext<HardmodeChartsRebuiltEvent> Rebuilt()
    {
        var context = new Mock<ConsumeContext<HardmodeChartsRebuiltEvent>>();
        context.SetupGet(c => c.Message).Returns(new HardmodeChartsRebuiltEvent(MixEnum.Phoenix2, 1, 1));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private static HardmodeSaga Build(IReadOnlyCollection<Chart> charts,
        IReadOnlyCollection<(Guid UserId, Chart Chart, int Score)> scores,
        Mock<IHardmodeRatingRepository> ratings, IReadOnlyCollection<Chart>? qualifying = null,
        IReadOnlyCollection<(Chart Chart, int Score)>? bests = null)
    {
        var list = (qualifying ?? charts)
            .Select(c => new HardmodeChartEntry(c.Id, c.Type, (int)c.Level, 0, 0, 40, 10)).ToArray();
        var reader = new Mock<IHardmodeChartReader>();
        reader.Setup(r => r.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var chartRepository = new Mock<IChartRepository>();
        chartRepository.Setup(r => r.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);

        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetScores(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, ChartType type, DifficultyLevel level, CancellationToken _) =>
                scores.Where(s => s.Chart.Type == type && s.Chart.Level == level)
                    .Select(s => (s.UserId, new RecordedPhoenixScore(s.Chart.Id, PhoenixScore.From(s.Score),
                        PhoenixPlate.MarvelousGame, false, At)))
                    .ToArray());
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix2, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bests ?? Array.Empty<(Chart, int)>())
                .Select(b => new RecordedPhoenixScore(b.Chart.Id, PhoenixScore.From(b.Score),
                    PhoenixPlate.MarvelousGame, false, At))
                .ToArray());

        ratings.Setup(r => r.GetBoard(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeBoardRow>());
        ratings.Setup(r => r.Clear(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ratings.Setup(r => r.Save(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeRatingRow>>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new HardmodeSaga(ratings.Object, reader.Object, scoreReader.Object, chartRepository.Object,
            NullLogger<HardmodeSaga>.Instance);
    }
}
