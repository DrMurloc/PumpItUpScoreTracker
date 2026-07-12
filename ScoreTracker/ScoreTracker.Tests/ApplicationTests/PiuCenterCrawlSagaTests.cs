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
    public async Task CaseTwinKeysAreNotTreatedAsNewAliases()
    {
        // piucenter's table carries case-twin junk rows ("..._s19_ARCADE" next to
        // "..._S19_ARCADE"); the SQL unique index compares case-insensitively, so
        // treating the twin as a new key blows up the insert (field-test 2026-07-11).
        var chart = new ChartBuilder().Build();
        SetupDefaults(new[] { chart },
            new[]
            {
                Listing("Fallen_Angel_-_DM_Ashura_S19_ARCADE", level: 19),
                Listing("Fallen_Angel_-_DM_Ashura_s19_ARCADE", level: 19)
            },
            new[]
            {
                new ExternalChartAlias("Fallen_Angel_-_DM_Ashura_S19_ARCADE", chart.Id,
                    ExternalAliasStatus.Manual, Now)
            },
            new[] { new ChartSkillMetric(chart.Id, PiuCenterMetrics.DataVersion, 50726m, null) });

        await Consume();

        _aliases.Verify(a => a.SaveAliases(It.IsAny<string>(), It.IsAny<IEnumerable<ExternalChartAlias>>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            new[] { "run" }, true, 12.0m, "12th notes @ 240 bpm", "D20");
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
    public async Task SnapshotImportBanksMetricsAndFlipsFromTheZipWithoutAnyHttp()
    {
        // The zero-crawl bootstrap: a zipped data release runs the same pipeline —
        // alias reconcile, metric banking (stamped with the zip's version so the
        // weekly crawl stays a no-op), skill flip — with the client never touched.
        var chart = new ChartBuilder().WithSongName("Slam").WithArtist("Novasonic").WithLevel(7).Build();
        var key = "Slam_-_Novasonic_S7_ARCADE";
        var storage = new List<ChartSkillMetric>();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _charts.Setup(c => c.GetChartSkills(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartSkillsRecord>());
        _aliases.Setup(a => a.GetAliases(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExternalChartAlias>());
        _metrics.Setup(m => m.ReplaceChartMetrics(chart.Id, PiuCenterMetrics.Source,
                It.IsAny<IEnumerable<ChartSkillMetric>>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, string _, IEnumerable<ChartSkillMetric> rows, CancellationToken _) =>
                storage.AddRange(rows))
            .Returns(Task.CompletedTask);
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), PiuCenterMetrics.Source,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storage.ToArray());

        var zip = BuildSnapshotZip(new Dictionary<string, string>
        {
            ["version.txt"] = "050726",
            ["page-content/chart-table.json"] =
                $"[{{\"name\": \"{key}\", \"sord\": \"singles\", \"level\": 7, \"pack\": \"S.E.~EXTRA\", " +
                "\"skills\": [\"drill\"], \"NPS\": 4.4, \"BPM info\": \"8th notes @ 132 bpm\", " +
                "\"Sustain time\": 5, \"Total time under tension\": 5}]",
            ["page-content/stepchart-skills.json"] = "[{}, {}]",
            ["page-content/tierlists.json"] = "{}",
            [$"{key}.json"] =
                "[[], [], {\"chart_skill_summary\": [\"drill\"], \"Segment metadata\": " +
                "[{\"level\": 5.0, \"Skill badges\": [\"twist_90\", \"drill\"], \"rare skills\": []}, " +
                "{\"level\": 6.0, \"Skill badges\": [\"twist_90\"], \"rare skills\": []}], " +
                "\"nps_summary\": 4.4}]"
        });

        var context = new Mock<ConsumeContext<ImportPiuCenterSnapshotCommand>>();
        context.SetupGet(c => c.Message).Returns(new ImportPiuCenterSnapshotCommand(zip));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        await BuildSaga().Consume(context.Object);

        // Auto-matched from the zip's table, banked with the zip's version, flipped.
        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
                list.Single().ExternalKey == key && list.Single().ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(storage, r => r.MetricName == PiuCenterMetrics.DataVersion && r.Value == 50726m);
        Assert.Contains(storage, r => r.MetricName == "badge_fraction:twist_90" && r.Value == 1m);
        _charts.Verify(c => c.SaveChartSkills(It.Is<ChartSkillsRecord>(r =>
                r.ChartId == chart.Id && r.HighlightsSkill.Contains(Skill.Drills) &&
                r.ContainsSkills.Contains(Skill.Twists)),
            It.IsAny<CancellationToken>()), Times.Once);
        _piuCenter.VerifyNoOtherCalls();
    }

    private static byte[] BuildSnapshotZip(IReadOnlyDictionary<string, string> entries)
    {
        using var memory = new System.IO.MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memory,
                   System.IO.Compression.ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new System.IO.StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return memory.ToArray();
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
