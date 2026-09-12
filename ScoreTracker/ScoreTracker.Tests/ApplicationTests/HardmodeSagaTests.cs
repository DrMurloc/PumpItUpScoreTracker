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

    [Fact]
    public async Task TheBoardTotalIsTheBestFiftyAndAgreesWithThePage()
    {
        // Sixty qualifying charts, so ten of them are surplus to a fifty-chart pool.
        var charts = Enumerable.Range(0, 60).Select(_ => Qualifying(21, ChartType.Single)).ToArray();
        var user = Guid.NewGuid();
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(charts, charts.Select((c, i) => (user, c, 999_000 - i * 1_000)).ToArray(), ratings,
            qualifying: charts, bests: charts.Select((c, i) => (c, 999_000 - i * 1_000)).ToArray());
        var saved = Captured(ratings);

        await saga.Consume(Rebuilt());
        var page = await saga.Handle(new GetHardmodePageQuery(user, MixEnum.Phoenix2), CancellationToken.None);

        var row = Assert.Single(saved);
        Assert.Equal(50, row.Held);
        Assert.Equal(50, row.SinglesHeld);
        // The board and the page price the same records off the same list, so they must not
        // disagree. The board summed EVERY qualifying chart a player had scored, which is not a
        // pool at all: it read three times high, and above the player's own PUMBILITY, which the
        // subset relationship makes impossible.
        Assert.Equal(page.Combined.Total, row.Combined, 6);
        // ...and there really was surplus to leave out, so the equality above is not vacuous.
        Assert.Equal(10, page.ScoredOutsidePool.Count);
        Assert.True(page.Pool.Sum(p => p.Value) + page.ScoredOutsidePool.Sum(p => p.Value) > row.Combined);
    }

    [Fact]
    public async Task EachPoolCarriesItsOwnHeldCount()
    {
        var singles = Enumerable.Range(0, 60).Select(_ => Qualifying(21, ChartType.Single)).ToArray();
        var doubles = Enumerable.Range(0, 12).Select(_ => Qualifying(21, ChartType.Double)).ToArray();
        var user = Guid.NewGuid();
        var all = singles.Concat(doubles).ToArray();
        var ratings = new Mock<IHardmodeRatingRepository>();
        var saga = Build(all, all.Select((c, i) => (user, c, 999_000 - i * 500)).ToArray(), ratings,
            qualifying: all);
        var saved = Captured(ratings);

        await saga.Consume(Rebuilt());

        var row = Assert.Single(saved);
        Assert.Equal(50, row.Held);
        Assert.Equal(50, row.SinglesHeld);
        // Twelve, not fifty. One shared count printed "50 / 50" on the doubles tab for a player
        // holding twelve doubles charts, because the combined pool happened to be full.
        Assert.Equal(12, row.DoublesHeld);
        // The combined pool draws its fifty from the union, so it is never worth less than one type's.
        Assert.True(row.Combined >= row.Singles);
    }

    /// <summary>
    ///     Captures what the reprice wrote. Call it AFTER <see cref="Build" />, whose own Save
    ///     setup would otherwise replace this one.
    /// </summary>
    private static List<HardmodeRatingRow> Captured(Mock<IHardmodeRatingRepository> ratings)
    {
        var saved = new List<HardmodeRatingRow>();
        ratings.Setup(r => r.Save(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeRatingRow>>(),
                It.IsAny<CancellationToken>()))
            .Callback((MixEnum _, IReadOnlyCollection<HardmodeRatingRow> rows, CancellationToken _) =>
                saved.AddRange(rows))
            .Returns(Task.CompletedTask);
        return saved;
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
