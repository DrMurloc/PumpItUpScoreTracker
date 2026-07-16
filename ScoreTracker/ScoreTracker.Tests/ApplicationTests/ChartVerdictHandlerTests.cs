using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ChartVerdictHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ITierListRepository> _tierLists = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IPlayerStatsReader> _playerStats = new();
    private readonly Mock<IMediator> _mediator = new();

    private ChartVerdictHandler BuildHandler()
    {
        return new ChartVerdictHandler(_charts.Object, _tierLists.Object, _scores.Object, _playerStats.Object,
            _mediator.Object, new MemoryCache(new MemoryCacheOptions()), FakeDateTime.At(Now).Object);
    }

    private void SetupWorld(Guid chartId)
    {
        var phoenixChart = new ChartBuilder().WithId(chartId).WithSongName("Baroque Virus")
            .WithType(ChartType.Double).WithLevel(20).WithOriginalMix(MixEnum.XX).Build();
        var xxChart = new ChartBuilder().WithId(chartId).WithSongName("Baroque Virus")
            .WithType(ChartType.Double).WithLevel(19).WithMix(MixEnum.XX).WithOriginalMix(MixEnum.XX).Build();
        _charts.Setup(c => c.GetChart(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenixChart);
        // History reads the flat mix-level map: this chart was D19 in XX, D20 in Phoenix.
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (chartId, MixEnum.XX, 19), (chartId, MixEnum.Phoenix, 20) });
        _charts.Setup(c => c.GetChartLetterGradeDifficulties(It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartLetterGradeDifficulty>());
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n.ToString() == "Pass Count"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Pass Count", chartId, TierListCategory.VeryHard, 1) });
        _tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, It.Is<Name>(n => n.ToString() == "Scores"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Scores", chartId, TierListCategory.Easy, 1) });
        // Three scorers on this chart (one broken), plus a same-folder record for another
        // chart that must not leak into this chart's population.
        var scorers = Enumerable.Range(1, 3)
            .Select(i => new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)).ToArray();
        _scores.Setup(s => s.GetScores(MixEnum.Phoenix, ChartType.Double, It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new (Guid, RecordedPhoenixScore)[]
            {
                (scorers[0], new RecordedPhoenixScore(chartId, PhoenixScore.From(986_120), null, false, Now)),
                (scorers[1], new RecordedPhoenixScore(chartId, PhoenixScore.From(920_000), null, false, Now)),
                (scorers[2], new RecordedPhoenixScore(chartId, PhoenixScore.From(880_000), null, true, Now)),
                (scorers[0], new RecordedPhoenixScore(Guid.NewGuid(), PhoenixScore.From(999_000), null, false, Now))
            });
        _playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerStatsRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>
            {
                [chartId] = new[] { new ChartSkillChipRecord(Skill.Stamina, true, 0.5m) }
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysisQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChartStepAnalysisRecord?)null);
    }

    [Fact]
    public async Task HandleAssemblesTheEvidenceIntoFacets()
    {
        var chartId = Guid.NewGuid();
        SetupWorld(chartId);

        var facets = await BuildHandler().Handle(new GetChartVerdictQuery(chartId), CancellationToken.None);

        var passVsScore = facets.OfType<PassVsScoreVerdict>().Single();
        Assert.Equal(TierListCategory.VeryHard, passVsScore.PassTier);
        Assert.Equal(TierListCategory.Easy, passVsScore.ScoreTier);

        var history = facets.OfType<HistoryVerdict>().Single();
        Assert.Equal(MixEnum.XX, history.DebutMix);
        Assert.Equal(new[] { 19, 20 }, history.Levels.Select(l => l.Level).ToArray());

        var fingerprint = facets.OfType<StyleFingerprintVerdict>().Single();
        Assert.Equal(Skill.Stamina, fingerprint.TopSkills.Single().Skill);

        // Three records on this chart (the fourth belongs to another chart), two clears.
        var population = facets.OfType<PopulationVerdict>().Single();
        Assert.Equal(3, population.ScoresTracked);
        Assert.Equal(2 / 3.0, population.PassRate, 1e-9);
    }

    [Fact]
    public async Task SecondCallServesFromTheCacheWithoutRereadingPorts()
    {
        var chartId = Guid.NewGuid();
        SetupWorld(chartId);
        var handler = BuildHandler();

        var first = await handler.Handle(new GetChartVerdictQuery(chartId), CancellationToken.None);
        var second = await handler.Handle(new GetChartVerdictQuery(chartId), CancellationToken.None);

        Assert.Equal(first.Count, second.Count);
        _charts.Verify(c => c.GetChart(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
