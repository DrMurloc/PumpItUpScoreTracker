using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ChartSimilaritySagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IChartScoringLevelRepository> _scoringLevels = new();
    private readonly Mock<IChartSimilarityRepository> _similarity = new();

    private ChartSimilaritySaga BuildSaga()
    {
        return new ChartSimilaritySaga(_charts.Object, _mediator.Object, _similarity.Object,
            _scoringLevels.Object, FakeDateTime.At(Now).Object);
    }

    /// <summary>Charts with no scoring level are gated on the folder alone — the default here.</summary>
    private void SetupScoringLevels(IDictionary<Guid, double>? levels = null)
    {
        _scoringLevels.Setup(s => s.GetScoringLevels(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels ?? new Dictionary<Guid, double>());
    }

    private static ConsumeContext<RecalculateChartSimilarityCommand> Context(
        MixEnum mix = MixEnum.Phoenix)
    {
        var context = new Mock<ConsumeContext<RecalculateChartSimilarityCommand>>();
        context.SetupGet(c => c.Message).Returns(new RecalculateChartSimilarityCommand(mix));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private void SetupBadges(IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>> badges)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartBadgeCoverageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badges);
    }

    private void SetupStepAnalyses(IReadOnlyDictionary<Guid, ChartStepAnalysisRecord> analyses)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
    }

    private static ChartStepAnalysisRecord Analysis(decimal nps, decimal? sustain = null, decimal? tension = null)
    {
        return new ChartStepAnalysisRecord(Array.Empty<string>(), new Dictionary<string, decimal>(),
            nps, sustain, tension, DifficultyPrediction: null, ExternalKey: null);
    }

    [Fact]
    public async Task GetSimilarChartsReturnsStoredEdgesInStoredOrder()
    {
        var chartId = Guid.NewGuid();
        var first = new ChartSimilarityEdge(Guid.NewGuid(), 0.9, 1.0, 0.6,
            new[] { new SharedBadgeCoverage("bracket", 0.5) });
        var second = new ChartSimilarityEdge(Guid.NewGuid(), 0.7, 0.8, 0.5,
            Array.Empty<SharedBadgeCoverage>());
        _similarity.Setup(s => s.GetEdges(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var result = await BuildSaga()
            .Handle(new GetSimilarChartsQuery(chartId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(first.SimilarChartId, result[0].ChartId);
        Assert.Equal(0.9, result[0].Score);
        Assert.Equal(1.0, result[0].SkillScore);
        Assert.Equal(0.6, result[0].IntensityScore);
        var badge = Assert.Single(result[0].SharedBadges);
        Assert.Equal("bracket", badge.Badge);
        Assert.Equal(0.5, badge.Coverage);
        Assert.Equal(second.SimilarChartId, result[1].ChartId);
        Assert.Equal(0.8, result[1].SkillScore);
        Assert.Equal(0.5, result[1].IntensityScore);
        Assert.Empty(result[1].SharedBadges);
    }

    [Fact]
    public async Task ConsumeRebuildsEdgesWholesaleForEverySimilarityChartAndSkipsCoOp()
    {
        var chartA = new ChartBuilder().WithSongName("Song A").WithType(ChartType.Single).WithLevel(20).Build();
        var chartB = new ChartBuilder().WithSongName("Song B").WithType(ChartType.Single).WithLevel(20).Build();
        var coOp = new ChartBuilder().WithSongName("Song C").WithType(ChartType.CoOp).WithLevel(3).Build();
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chartA, chartB, coOp });
        // Both singles carry the same badge at full coverage and the same NPS: identical
        // evidence on both mandatory signals.
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>
        {
            [chartA.Id] = new Dictionary<string, double> { ["bracket"] = 1.0 },
            [chartB.Id] = new Dictionary<string, double> { ["bracket"] = 1.0 }
        });
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>
        {
            [chartA.Id] = Analysis(10),
            [chartB.Id] = Analysis(10)
        });

        await BuildSaga().Consume(Context());

        // One edge each way; the wholesale rewrite carries the clock's timestamp; Co-Op
        // never participates.
        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartA.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 1 && e[0].SimilarChartId == chartB.Id),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartB.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 1 && e[0].SimilarChartId == chartA.Id),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        _similarity.Verify(s => s.ReplaceEdges(It.IsAny<MixEnum>(), coOp.Id,
            It.IsAny<IReadOnlyList<ChartSimilarityEdge>>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheSpikesAreTheTensionThatIsNotGrind()
    {
        // Two 120-second charts spending the identical 60 seconds under tension, composed
        // oppositely: A never lets up (sustain 60 of 60, the Gargoyle - FULL SONG - D25
        // shape) and B is nothing but spikes (sustain 0, so all 60 seconds are burst).
        // Time under tension cannot tell them apart — it is the same number — which is
        // exactly why carrying it alongside sustain counted the grind twice and gave the
        // spikes no dimension at all. On (nps, susFrac, tensionFrac) this pair read
        // 0.7778, its identical tension arguing the two were alike; decomposed, it reads
        // 0.4152.
        var grind = new ChartBuilder().WithSongName("Grind").WithType(ChartType.Double).WithLevel(21).Build();
        var spikes = new ChartBuilder().WithSongName("Spikes").WithType(ChartType.Double).WithLevel(21).Build();
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { grind, spikes });
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>
        {
            [grind.Id] = new Dictionary<string, double> { ["bracket"] = 1.0 },
            [spikes.Id] = new Dictionary<string, double> { ["bracket"] = 1.0 }
        });
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>
        {
            [grind.Id] = Analysis(10, sustain: 60, tension: 60),
            [spikes.Id] = Analysis(10, sustain: 0, tension: 60)
        });

        await BuildSaga().Consume(Context());

        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, grind.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e =>
                e.Count == 1 && Math.Abs(e[0].IntensityScore - 0.4152436465) < 1e-6),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     An anchor plus three targets spread over D20–D23, so a live search has something
    ///     to include and something to leave out. Every chart carries the same badge and
    ///     NPS, so the scores are flat and the assertions are about *which charts were
    ///     compared*, never about ranking.
    /// </summary>
    private (Chart Anchor, Chart[] Targets) SetupLivePool()
    {
        var anchor = new ChartBuilder().WithSongName("Anchor").WithType(ChartType.Double).WithLevel(21)
            .WithStepArtist("SPHAM").Build();
        var nearby = new ChartBuilder().WithSongName("Nearby").WithType(ChartType.Double).WithLevel(22)
            .WithStepArtist("SPHAM").Build();
        var farUp = new ChartBuilder().WithSongName("Far Up").WithType(ChartType.Double).WithLevel(23)
            .WithStepArtist("NIMGO").Build();
        var farDown = new ChartBuilder().WithSongName("Far Down").WithType(ChartType.Double).WithLevel(20)
            .WithStepArtist("NIMGO").Build();
        var coOp = new ChartBuilder().WithSongName("Co-Op").WithType(ChartType.CoOp).WithLevel(3).Build();
        var all = new[] { anchor, nearby, farUp, farDown, coOp };
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);
        SetupBadges(all.ToDictionary(c => c.Id,
            c => (IReadOnlyDictionary<string, double>)new Dictionary<string, double> { ["bracket"] = 1.0 }));
        SetupStepAnalyses(all.ToDictionary(c => c.Id, _ => Analysis(10)));
        return (anchor, new[] { nearby, farUp, farDown });
    }

    [Fact]
    public async Task AFilteredSearchDefaultsToTheSameFolderReachTheGraphPrecalculates()
    {
        var (anchor, targets) = SetupLivePool();

        var result = await BuildSaga().Handle(new GetFilteredSimilarChartsQuery(anchor.Id),
            CancellationToken.None);

        // D20 and D22 are within a folder of D21; the D23 is not, and Co-Op is a different
        // pool entirely.
        var inReach = targets.Where(t => Math.Abs(t.Level - 21) <= 1).ToArray();
        Assert.Equal(inReach.Length, result.ChartsCompared);
        Assert.Equal(inReach.Select(t => t.Id).OrderBy(id => id),
            result.Matches.Select(m => m.ChartId).OrderBy(id => id));
    }

    [Fact]
    public async Task AFilteredSearchReachesOutsideThePrecalculatedWindowWhenAsked()
    {
        // "I liked this D18, what D23s play like it" — the level range is the reader's,
        // and the whole reason this query exists rather than reading the stored graph.
        var (anchor, targets) = SetupLivePool();
        var onlyD23 = targets.Single(t => t.Level == 23);

        var result = await BuildSaga().Handle(
            new GetFilteredSimilarChartsQuery(anchor.Id, MinLevel: 23, MaxLevel: 23), CancellationToken.None);

        Assert.Equal(1, result.ChartsCompared);
        var match = Assert.Single(result.Matches);
        Assert.Equal(onlyD23.Id, match.ChartId);
    }

    [Fact]
    public async Task AFilteredSearchReportsWhatItComparedEvenWhenItNarrowsToNothingWorthShowing()
    {
        // Compared is what the filter selected, not what scored — it is what turns
        // "1 match" from a bug report into a sentence.
        var (anchor, targets) = SetupLivePool();
        var onlyD22 = targets.Single(t => t.Level == 22);

        var result = await BuildSaga().Handle(
            new GetFilteredSimilarChartsQuery(anchor.Id, MinLevel: 22, MaxLevel: 22), CancellationToken.None);

        Assert.Equal(1, result.ChartsCompared);
        Assert.Equal(onlyD22.Id, Assert.Single(result.Matches).ChartId);
    }

    [Fact]
    public async Task AnUnmeasuredChartFiltersAtItsListedLevelRatherThanFallingOutOfTheRange()
    {
        // The rest of the app reports the listed level for a chart nothing has measured, so
        // a scoring-level filter has to agree with it — otherwise switching one on would
        // silently drop the ~13% that have no measurement, and the count the reader watched
        // while dragging would not be the list they got back.
        var (anchor, targets) = SetupLivePool();
        var measured = targets.Single(t => t.Level == 22);
        SetupScoringLevels(new Dictionary<Guid, double> { [measured.Id] = 25.0 });

        // Every unmeasured chart sits at its folder, so D20–D22 answers this; the one chart
        // that HAS a measurement answers at 25.0 and is out.
        var result = await BuildSaga().Handle(
            new GetFilteredSimilarChartsQuery(anchor.Id, MinScoringLevel: 19.5, MaxScoringLevel: 22.5),
            CancellationToken.None);

        Assert.Equal(targets.Where(t => t.Level == 20 && t.Id != measured.Id).Select(t => t.Id).OrderBy(id => id),
            result.Matches.Select(m => m.ChartId).OrderBy(id => id));
    }

    [Fact]
    public async Task AChartWithNoNpsCannotAnswerAnNpsFilter()
    {
        // There is no listed value to fall back to the way a scoring level has its folder,
        // and admitting it anyway would put a chart of unknown speed inside a speed filter.
        var (anchor, targets) = SetupLivePool();
        var fast = targets.Single(t => t.Level == 22);
        var silent = targets.Single(t => t.Level == 20);
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>
        {
            [anchor.Id] = Analysis(10),
            [fast.Id] = Analysis(12)
            // silent is absent: the crawl banked no analysis for it.
        });

        var result = await BuildSaga().Handle(
            new GetFilteredSimilarChartsQuery(anchor.Id, MinNps: 11, MaxNps: 13), CancellationToken.None);

        Assert.Equal(1, result.ChartsCompared);
        Assert.Equal(fast.Id, Assert.Single(result.Matches).ChartId);
        Assert.DoesNotContain(result.Matches, m => m.ChartId == silent.Id);
    }

    [Fact]
    public async Task AFilteredSearchOnAChartTheCrawlNeverCoveredComparesNothing()
    {
        var missing = new ChartBuilder().WithSongName("Ghost").WithType(ChartType.Double).WithLevel(21).Build();
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>());
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>());

        var result = await BuildSaga().Handle(new GetFilteredSimilarChartsQuery(missing.Id),
            CancellationToken.None);

        Assert.Equal(0, result.ChartsCompared);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task TheLeastSimilarChartsAreWorstFirstRatherThanBest()
    {
        // The graph banks the twenty nearest, so the furthest are what ranking never keeps
        // — they have to be computed. Anchor is all brackets; the joke leads with the chart
        // that shares none of them.
        var anchor = new ChartBuilder().WithSongName("Anchor").WithType(ChartType.Double).WithLevel(21).Build();
        var alike = new ChartBuilder().WithSongName("Alike").WithType(ChartType.Double).WithLevel(21).Build();
        var middling = new ChartBuilder().WithSongName("Middling").WithType(ChartType.Double).WithLevel(21).Build();
        var opposite = new ChartBuilder().WithSongName("Opposite").WithType(ChartType.Double).WithLevel(21).Build();
        var all = new[] { anchor, alike, middling, opposite };
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(all);
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>
        {
            [anchor.Id] = new Dictionary<string, double> { ["bracket"] = 1.0 },
            [alike.Id] = new Dictionary<string, double> { ["bracket"] = 0.9 },
            [middling.Id] = new Dictionary<string, double> { ["bracket"] = 0.4, ["twist_90"] = 0.6 },
            [opposite.Id] = new Dictionary<string, double> { ["twist_90"] = 1.0 }
        });
        SetupStepAnalyses(all.ToDictionary(c => c.Id, _ => Analysis(10)));

        var result = await BuildSaga().Handle(new GetLeastSimilarChartsQuery(anchor.Id), CancellationToken.None);

        // Worst first — the shelf always opens with the most of whatever it is showing.
        Assert.Equal(new[] { opposite.Id, middling.Id, alike.Id }, result.Select(r => r.ChartId));
    }

    [Fact]
    public async Task TheLeastSimilarChartsStopAtTheCountAsked()
    {
        var (anchor, _) = SetupLivePool();

        var result = await BuildSaga().Handle(new GetLeastSimilarChartsQuery(anchor.Id, Count: 1),
            CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task TheLeastSimilarChartsAreEmptyForAChartTheCrawlNeverCovered()
    {
        var missing = new ChartBuilder().WithSongName("Ghost").WithType(ChartType.Double).WithLevel(21).Build();
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>());
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>());

        Assert.Empty(await BuildSaga().Handle(new GetLeastSimilarChartsQuery(missing.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeWritesEmptyEdgeSetsForChartsTheCrawlNeverCovered()
    {
        // No badges, no step analysis — nothing to compare, so no pair earns an edge. The
        // rewrite still happens, so edges from a previous run are cleared rather than left
        // to rot.
        var chartA = new ChartBuilder().WithSongName("Song A").WithType(ChartType.Double).WithLevel(15).Build();
        var chartB = new ChartBuilder().WithSongName("Song B").WithType(ChartType.Double).WithLevel(15).Build();
        SetupScoringLevels();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chartA, chartB });
        SetupBadges(new Dictionary<Guid, IReadOnlyDictionary<string, double>>());
        SetupStepAnalyses(new Dictionary<Guid, ChartStepAnalysisRecord>());

        await BuildSaga().Consume(Context());

        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartA.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 0),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartB.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 0),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }
}
