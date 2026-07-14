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
using ScoreTracker.Domain.Records;
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
    private readonly Mock<ITierListRepository> _tierLists = new();
    private readonly Mock<IChartScoringLevelRepository> _scoringLevels = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IPlayerStatsReader> _playerStats = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IChartSimilarityRepository> _similarity = new();

    private ChartSimilaritySaga BuildSaga()
    {
        return new ChartSimilaritySaga(_charts.Object, _tierLists.Object, _scoringLevels.Object,
            _scores.Object, _playerStats.Object, _mediator.Object, _similarity.Object,
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

    [Fact]
    public async Task GetSimilarChartsReturnsStoredEdgesInStoredOrder()
    {
        var chartId = Guid.NewGuid();
        var first = new ChartSimilarityEdge(Guid.NewGuid(), 0.9, 1.0, null, 0.6, null, 0.3, 42);
        var second = new ChartSimilarityEdge(Guid.NewGuid(), 0.7, null, 0.8, null, 0.5, 0.0, 0);
        _similarity.Setup(s => s.GetEdges(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var result = await BuildSaga()
            .Handle(new GetSimilarChartsQuery(chartId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(first.SimilarChartId, result[0].ChartId);
        Assert.Equal(0.9, result[0].Score);
        Assert.Equal(1.0, result[0].StyleScore);
        Assert.Equal(0.6, result[0].PlayersScore);
        Assert.Equal(42, result[0].SharedScorers);
        Assert.Equal(second.SimilarChartId, result[1].ChartId);
        Assert.Null(result[1].StyleScore);
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
        _charts.Setup(c => c.GetChartLetterGradeDifficulties(It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartLetterGradeDifficulty>());
        // Style evidence: both singles share one banked skill at full coverage.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>
            {
                [chartA.Id] = new[] { new ChartSkillChipRecord(Skill.Stamina, true, 1.0m) },
                [chartB.Id] = new[] { new ChartSkillChipRecord(Skill.Stamina, true, 1.0m) }
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartStepAnalysisRecord>());
        // Behavior evidence: both sit Medium on the pass tier list; the Scores list is empty.
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n.ToString() == "Pass Count"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SongTierListEntry("Pass Count", chartA.Id, TierListCategory.Medium, 1),
                new SongTierListEntry("Pass Count", chartB.Id, TierListCategory.Medium, 2)
            });
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n.ToString() == "Scores"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        _scoringLevels.Setup(s => s.GetScoringLevels(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Single, It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, ScoreTracker.Domain.Models.RecordedPhoenixScore)>());

        await BuildSaga().Consume(Context());

        // Two real signals (style + pass tier) → one edge each way; the wholesale rewrite
        // carries the clock's timestamp; Co-Op never participates.
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
    public async Task ConsumeWritesEmptyEdgeSetsForChartsWithoutEnoughEvidence()
    {
        // No skills, no tiers, no scores — no pair clears the two-non-meta-signals gate,
        // but the rewrite still happens so stale edges from a previous run are cleared.
        var chartA = new ChartBuilder().WithSongName("Song A").WithType(ChartType.Double).WithLevel(15).Build();
        var chartB = new ChartBuilder().WithSongName("Song B").WithType(ChartType.Double).WithLevel(15).Build();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chartA, chartB });
        _charts.Setup(c => c.GetChartLetterGradeDifficulties(It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartLetterGradeDifficulty>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartStepAnalysisRecord>());
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        _scoringLevels.Setup(s => s.GetScoringLevels(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Double, It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, ScoreTracker.Domain.Models.RecordedPhoenixScore)>());

        await BuildSaga().Consume(Context());

        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartA.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 0),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        _similarity.Verify(s => s.ReplaceEdges(MixEnum.Phoenix, chartB.Id,
            It.Is<IReadOnlyList<ChartSimilarityEdge>>(e => e.Count == 0),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }
}
