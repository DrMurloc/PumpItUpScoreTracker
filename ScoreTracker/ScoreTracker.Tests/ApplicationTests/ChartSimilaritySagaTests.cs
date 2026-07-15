using System;
using System.Collections.Generic;
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
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ChartSimilaritySagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IChartSimilarityRepository> _similarity = new();

    private ChartSimilaritySaga BuildSaga()
    {
        return new ChartSimilaritySaga(_charts.Object, _mediator.Object, _similarity.Object,
            FakeDateTime.At(Now).Object);
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
        var first = new ChartSimilarityEdge(Guid.NewGuid(), 0.9, 1.0, 0.6);
        var second = new ChartSimilarityEdge(Guid.NewGuid(), 0.7, 0.8, 0.5);
        _similarity.Setup(s => s.GetEdges(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var result = await BuildSaga()
            .Handle(new GetSimilarChartsQuery(chartId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(first.SimilarChartId, result[0].ChartId);
        Assert.Equal(0.9, result[0].Score);
        Assert.Equal(1.0, result[0].SkillScore);
        Assert.Equal(0.6, result[0].IntensityScore);
        Assert.Equal(second.SimilarChartId, result[1].ChartId);
        Assert.Equal(0.8, result[1].SkillScore);
        Assert.Equal(0.5, result[1].IntensityScore);
    }

    [Fact]
    public async Task ConsumeRebuildsEdgesWholesaleForEverySimilarityChartAndSkipsCoOp()
    {
        var chartA = new ChartBuilder().WithSongName("Song A").WithType(ChartType.Single).WithLevel(20).Build();
        var chartB = new ChartBuilder().WithSongName("Song B").WithType(ChartType.Single).WithLevel(20).Build();
        var coOp = new ChartBuilder().WithSongName("Song C").WithType(ChartType.CoOp).WithLevel(3).Build();
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

    [Fact]
    public async Task ConsumeWritesEmptyEdgeSetsForChartsTheCrawlNeverCovered()
    {
        // No badges, no step analysis — nothing to compare, so no pair earns an edge. The
        // rewrite still happens, so edges from a previous run are cleared rather than left
        // to rot.
        var chartA = new ChartBuilder().WithSongName("Song A").WithType(ChartType.Double).WithLevel(15).Build();
        var chartB = new ChartBuilder().WithSongName("Song B").WithType(ChartType.Double).WithLevel(15).Build();
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
