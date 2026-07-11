using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Messages;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PiuCenterCrawlSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IExternalChartAliasRepository> _aliases = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();
    private readonly Mock<IPiuCenterClient> _piuCenter = new();

    private PiuCenterCrawlSaga BuildSaga()
    {
        return new PiuCenterCrawlSaga(_piuCenter.Object, _aliases.Object, _metrics.Object, _charts.Object,
            FakeDateTime.At(Now).Object, NullLogger<PiuCenterCrawlSaga>.Instance);
    }

    private Task Consume()
    {
        var context = new Mock<ConsumeContext<CrawlPiuCenterCommand>>();
        context.SetupGet(c => c.Message).Returns(new CrawlPiuCenterCommand());
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return BuildSaga().Consume(context.Object);
    }

    private void SetupDefaults(IEnumerable<Chart> charts, IEnumerable<PiuCenterChartListing> table,
        IEnumerable<ExternalChartAlias> aliases, IEnumerable<ChartSkillMetric>? existingMetrics = null,
        PiuCenterChartPage? page = null)
    {
        _piuCenter.Setup(p => p.GetDataVersion(It.IsAny<CancellationToken>())).ReturnsAsync("050726");
        _piuCenter.Setup(p => p.GetChartTable(It.IsAny<CancellationToken>()))
            .ReturnsAsync(table.ToArray());
        _piuCenter.Setup(p => p.GetPracticeLists(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PiuCenterPracticeEntry>());
        _piuCenter.Setup(p => p.GetDifficultyPredictions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        _piuCenter.Setup(p => p.GetChartPage(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts.ToArray());
        _charts.Setup(c => c.GetChartSkills(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartSkillsRecord>());
        _aliases.Setup(a => a.GetAliases(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aliases.ToArray());
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), PiuCenterMetrics.Source,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((existingMetrics ?? Array.Empty<ChartSkillMetric>()).ToArray());
    }

    private static PiuCenterChartListing Listing(string key, ChartType type = ChartType.Single, int level = 15,
        string variant = "ARCADE")
    {
        return new PiuCenterChartListing(key, type, level, "PHOENIX", variant, new[] { "run" }, 6.0m,
            "8th notes @ 160 bpm", 10, 40);
    }

    [Fact]
    public async Task NewTableKeysAutoMatchByNormalizedIdentityOrParkForAdminResolution()
    {
        var chart = new ChartBuilder().WithSongName("Allegro Più Mosso").WithArtist("DM Ashura").WithLevel(17)
            .Build();
        SetupDefaults(new[] { chart },
            new[]
            {
                // Diacritic + separator differences must not break the match.
                Listing("Allegro_Piu_Mosso_-_DM_Ashura_S17_ARCADE", level: 17),
                Listing("Some_Unknown_Song_-_Nobody_S15_ARCADE")
            },
            Array.Empty<ExternalChartAlias>());

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
            list.Any(x => x.ExternalKey == "Allegro_Piu_Mosso_-_DM_Ashura_S17_ARCADE" && x.ChartId == chart.Id &&
                          x.Status == ExternalAliasStatus.Auto) &&
            list.Any(x => x.ExternalKey == "Some_Unknown_Song_-_Nobody_S15_ARCADE" && x.ChartId == null)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotFoundCandidateFlipsToAutoWhenItsKeyAppearsUpstream()
    {
        var chart = new ChartBuilder().Build();
        var key = "Pandora_-_KARA_S15_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key) },
            new[] { new ExternalChartAlias(key, chart.Id, ExternalAliasStatus.NotFound, Now.AddMonths(-2)) });

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
                list.Single().ExternalKey == key && list.Single().Status == ExternalAliasStatus.Auto &&
                list.Single().ChartId == chart.Id && list.Single().LastCheckedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GapChartsGetTheirPageFetchedAndMetricsBanked()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build();
        var key = "Repentance_-_Abel_D20_ARCADE";
        var page = new PiuCenterChartPage(key, new[] { "bracket_drill", "bracket_run", "bracket" }, 8,
            new Dictionary<string, int> { ["twist_90"] = 4, ["drill"] = 2 },
            new Dictionary<string, int> { ["bracket drill-5"] = 2 },
            new[] { "run" }, 12.0m, "12th notes @ 240 bpm", "D20");
        SetupDefaults(new[] { chart }, new[] { Listing(key, ChartType.Double, 20) },
            new[] { new ExternalChartAlias(key, chart.Id, ExternalAliasStatus.Auto, Now) }, page: page);

        await Consume();

        _metrics.Verify(m => m.ReplaceChartMetrics(chart.Id, PiuCenterMetrics.Source,
            It.Is<IEnumerable<ChartSkillMetric>>(rows =>
                rows.Any(r => r.MetricName == PiuCenterMetrics.DataVersion && r.Value == 50726m) &&
                rows.Any(r => r.MetricName == "top3:bracket_drill" && r.Value == 1m) &&
                rows.Any(r => r.MetricName == "badge_fraction:twist_90" && r.Value == 0.5m) &&
                rows.Any(r => r.MetricName == "last_segment_badge:run")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChartsAlreadyOnTheCurrentDataReleaseAreNotRefetched()
    {
        var chart = new ChartBuilder().Build();
        var key = "Slam_-_Novasonic_S7_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key, level: 7) },
            new[] { new ExternalChartAlias(key, chart.Id, ExternalAliasStatus.Auto, Now) },
            new[] { new ChartSkillMetric(chart.Id, PiuCenterMetrics.DataVersion, 50726m, null) });

        await Consume();

        _piuCenter.Verify(p => p.GetChartPage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegenerationClearsTagsOnChartsPiuCenterHasNothingFor()
    {
        // The hand-tag purge: a chart with stored tags but no banked metrics gets an
        // empty record written (its rows only survive in ChartSkillArchive).
        var chart = new ChartBuilder().Build();
        SetupDefaults(new[] { chart }, Array.Empty<PiuCenterChartListing>(), Array.Empty<ExternalChartAlias>());
        _charts.Setup(c => c.GetChartSkills(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSkillsRecord(chart.Id, new[] { Skill.Gimmicks, Skill.Twists }, new[] { Skill.Gimmicks })
            });

        await Consume();

        _charts.Verify(c => c.SaveChartSkills(It.Is<ChartSkillsRecord>(r =>
                r.ChartId == chart.Id && !r.ContainsSkills.Any() && !r.HighlightsSkill.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegenerationWritesMappedTagsFromBankedMetrics()
    {
        var chart = new ChartBuilder().Build();
        var key = "Slam_-_Novasonic_S7_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key, level: 7) },
            new[] { new ExternalChartAlias(key, chart.Id, ExternalAliasStatus.Auto, Now) },
            new[]
            {
                new ChartSkillMetric(chart.Id, PiuCenterMetrics.DataVersion, 50726m, null),
                new ChartSkillMetric(chart.Id, "top3:drill", 1m, null),
                new ChartSkillMetric(chart.Id, "badge_fraction:twist_90", 0.6m, null)
            });

        await Consume();

        _charts.Verify(c => c.SaveChartSkills(It.Is<ChartSkillsRecord>(r =>
                r.ChartId == chart.Id && r.HighlightsSkill.Contains(Skill.Drills) &&
                r.ContainsSkills.Contains(Skill.Twists)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
