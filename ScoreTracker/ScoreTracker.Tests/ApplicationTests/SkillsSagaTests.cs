using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SkillsSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IExternalChartAliasRepository> _aliases = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();

    private SkillsSaga BuildSaga()
    {
        return new SkillsSaga(_charts.Object, _metrics.Object, _aliases.Object, FakeDateTime.At(Now).Object);
    }

    [Fact]
    public async Task GetChartSkillsDelegatesToRepository()
    {
        var skills = new List<ChartSkillsRecord>();
        _charts.Setup(c => c.GetChartSkills(It.IsAny<CancellationToken>())).ReturnsAsync(skills);

        var result = await BuildSaga().Handle(new GetChartSkillsQuery(), CancellationToken.None);

        Assert.Same(skills, result);
    }

    [Fact]
    public async Task StepAnalysisShapesBankedMetricsAndPrefersTheAliasKey()
    {
        var chartId = Guid.NewGuid();
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), "PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillMetric(chartId, "top3:bracket_drill", 1m, null),
                new ChartSkillMetric(chartId, "top3:bracket", 2m, null),
                new ChartSkillMetric(chartId, "badge_fraction:twist_90", 0.5m, null),
                new ChartSkillMetric(chartId, "nps", 12m, null)
            });
        _aliases.Setup(a => a.GetAliasForChart("PiuCenter", chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalChartAlias("Repentance_-_Abel_D20_ARCADE", chartId,
                ExternalAliasStatus.Auto, Now));

        var analysis = await BuildSaga().Handle(new GetChartStepAnalysisQuery(chartId), CancellationToken.None);

        Assert.NotNull(analysis);
        Assert.Equal(new[] { "bracket_drill", "bracket" }, analysis!.TopSkills);
        Assert.Equal(0.5m, analysis.BadgeFractions["twist_90"]);
        Assert.Equal(12m, analysis.Nps);
        Assert.Equal("Repentance_-_Abel_D20_ARCADE", analysis.ExternalKey);
    }

    [Fact]
    public async Task StepAnalysisIsNullWithoutBankedMetrics()
    {
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), "PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartSkillMetric>());

        var analysis = await BuildSaga().Handle(new GetChartStepAnalysisQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(analysis);
    }

    [Fact]
    public async Task SkillChipsOrderTopThreeFirstThenBySegmentCoverage()
    {
        var chartId = Guid.NewGuid();
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), "PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillMetric(chartId, "top3:drill", 1m, null),
                new ChartSkillMetric(chartId, "badge_fraction:drill", 0.8m, null),
                new ChartSkillMetric(chartId, "badge_fraction:twist_90", 0.6m, null),
                new ChartSkillMetric(chartId, "badge_fraction:jump", 0.55m, null),
                // Below its per-skill threshold — must not become a chip.
                new ChartSkillMetric(chartId, "badge_fraction:jack", 0.1m, null)
            });

        var chips = await BuildSaga().Handle(new GetChartSkillChipsQuery(new[] { chartId }),
            CancellationToken.None);

        var chart = chips[chartId];
        Assert.Equal(Skill.Drills, chart[0].Skill);
        Assert.True(chart[0].Highlighted);
        Assert.Equal(0.8m, chart[0].SegmentFraction);
        Assert.Equal(Skill.Twists, chart[1].Skill);
        Assert.False(chart[1].Highlighted);
        Assert.Equal(Skill.Jumps, chart[2].Skill);
        Assert.DoesNotContain(chart, c => c.Skill == Skill.Jacks);
    }

    [Fact]
    public async Task UnresolvedAliasesAreTheNullChartRows()
    {
        _aliases.Setup(a => a.GetAliases("PiuCenter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalChartAlias("Matched_-_Artist_S10_ARCADE", Guid.NewGuid(), ExternalAliasStatus.Auto, Now),
                new ExternalChartAlias("Parked_-_Artist_S11_ARCADE", null, ExternalAliasStatus.Auto, Now)
            });

        var unresolved = await BuildSaga().Handle(new GetUnresolvedAliasesQuery(), CancellationToken.None);

        var only = Assert.Single(unresolved);
        Assert.Equal("Parked_-_Artist_S11_ARCADE", only.ExternalKey);
    }

    [Fact]
    public async Task ResolvingAnAliasBindsItManuallyAtTheCurrentTime()
    {
        var chartId = Guid.NewGuid();

        await BuildSaga().Handle(
            new ResolveExternalAliasCommand("PiuCenter", "Parked_-_Artist_S11_ARCADE", chartId),
            CancellationToken.None);

        _aliases.Verify(a => a.ResolveAlias("PiuCenter", "Parked_-_Artist_S11_ARCADE", chartId, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
