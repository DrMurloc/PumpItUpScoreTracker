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
    private readonly Mock<IChartFolderBaselineRepository> _baselines = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IChartSkillMetricRepository> _metrics = new();
    private readonly Mock<IPiuCenterClient> _piuCenter = new();

    /// <summary>
    ///     Every ingestion path ends by rebuilding folder baselines, so its two reads answer
    ///     empty for tests that are not about them — a test asserting on the alias pass should
    ///     not have to know the baseline pass exists.
    /// </summary>
    public PiuCenterCrawlSagaTests()
    {
        _metrics.Setup(m => m.GetMetricsByChart(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>());
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, MixEnum, int, int?)>());
    }

    private readonly Mock<IChartStepChartRepository> _stepCharts = new();
    private readonly Mock<IStepFileStore> _stepStore = new();

    private PiuCenterCrawlSaga BuildSaga()
    {
        var ingest = new StepChartIngest(_stepCharts.Object, _stepStore.Object, _charts.Object,
            FakeDateTime.At(Now).Object, NullLogger<StepChartIngest>.Instance);
        return new PiuCenterCrawlSaga(_piuCenter.Object, _aliases.Object, _metrics.Object, _charts.Object,
            FakeDateTime.At(Now).Object, _baselines.Object, ingest, NullLogger<PiuCenterCrawlSaga>.Instance);
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
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts.ToArray());
        _aliases.Setup(a => a.GetAliases(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aliases.ToArray());
        _metrics.Setup(m => m.GetMetrics(It.IsAny<IEnumerable<Guid>>(), PiuCenterMetrics.Source,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((existingMetrics ?? Array.Empty<ChartSkillMetric>()).ToArray());
    }

    /// <summary>
    ///     §3.9. The hold share is the one per-mix number in the baseline sweep — the same chart
    ///     is a different fraction of holds in each catalog, because the judged count moves —
    ///     and a Phoenix 2 catalog whose count has not refilled from play yet borrows Phoenix
    ///     1's, the calculator's own fallback. Both mixes' folders must end up carrying a
    ///     measured hold_share row.
    /// </summary>
    [Fact]
    public async Task TheBaselineSweepDerivesHoldSharePerMixWithThePhoenixFallback()
    {
        var chartId = Guid.NewGuid();
        var chart = new ChartBuilder().WithId(chartId).WithType(ChartType.Double).WithLevel(20).Build();
        SetupDefaults(new[] { chart }, Array.Empty<PiuCenterChartListing>(),
            Array.Empty<ExternalChartAlias>());
        _metrics.Setup(m => m.GetMetricsByChart(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillMetric>>
            {
                [chartId] = new[]
                {
                    new ChartSkillMetric(chartId, PiuCenterMetrics.TapRows, 156m, null),
                    new ChartSkillMetric(chartId, PiuCenterMetrics.HoldTicks, 848m, null)
                }
            });
        _charts.Setup(c => c.GetChartMixLevels(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                (chartId, MixEnum.Phoenix, 20, (int?)1000),
                (chartId, MixEnum.Phoenix2, 20, (int?)null)
            });

        await Consume();

        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
            _baselines.Verify(b => b.ReplaceBaselines(mix, It.Is<IReadOnlyList<ChartFolderBaseline>>(rows =>
                    rows.Any(r => r.Badge == PiuCenterMetrics.HoldShare && r.PresentCount == 1 &&
                                  r.DrenchedCutoff == 0.844m)),
                It.IsAny<CancellationToken>()), Times.Once);
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

    /// <summary>
    ///     A parked key gets another go every run. Nothing did this, so every alias that failed
    ///     its first match was failing forever and a re-upload could not repair any of it —
    ///     including the 176 the Phoenix 2 catalog flip should have rebound (field test,
    ///     2026-08-26).
    /// </summary>
    [Fact]
    public async Task AParkedAliasIsRetriedAgainstTheCatalogOnEveryRun()
    {
        var chart = new ChartBuilder().WithSongName("Bad Apple!!").WithArtist("Masayoshi Minoshima")
            .WithLevel(17).Build();
        var key = "Bad_Apple!!_-_Masayoshi_Minoshima_S17_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key, level: 17) },
            new[] { new ExternalChartAlias(key, null, ExternalAliasStatus.Auto, Now.AddMonths(-2)) });

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
                list.Single().ExternalKey == key && list.Single().ChartId == chart.Id &&
                list.Single().LastCheckedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     An admin's row is not the auto-matcher's to touch — including a deliberate decision
    ///     that a key maps to nothing.
    /// </summary>
    [Fact]
    public async Task AManuallyParkedAliasIsLeftAlone()
    {
        var chart = new ChartBuilder().WithSongName("Bad Apple!!").WithArtist("Masayoshi Minoshima")
            .WithLevel(17).Build();
        var key = "Bad_Apple!!_-_Masayoshi_Minoshima_S17_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key, level: 17) },
            new[] { new ExternalChartAlias(key, null, ExternalAliasStatus.Manual, Now.AddMonths(-2)) });

        await Consume();

        _aliases.Verify(a => a.SaveAliases(It.IsAny<string>(), It.IsAny<IEnumerable<ExternalChartAlias>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     The two halves of the rejection, together. A key we refuse to ingest must not bind, and
    ///     must not hold the chart's one slot either — Gargoyle FULL SONG S21 sat on metrics from
    ///     before its v2 key was rejected, because the v1 key that replaces it could never claim a
    ///     chart the rejected key had already reserved.
    /// </summary>
    [Fact]
    public async Task ARejectedKeyNeitherBindsNorBlocksTheChartItNamed()
    {
        var chart = new ChartBuilder().WithSongName("Gargoyle - FULL SONG -").WithArtist("Sanxion7")
            .WithSongType(SongType.FullSong).WithLevel(21).Build();
        const string rejected = "Gargoyle_-_FULL_SONG_-_v2_-_Sanxion7_S21_FULLSONG";
        const string survivor = "Gargoyle_-_FULL_SONG_-_v1_-_Sanxion7_S21_INFOBAR_TITLE_FULLSONG";
        SetupDefaults(new[] { chart },
            new[]
            {
                Listing(rejected, level: 21, variant: "FULLSONG"),
                Listing(survivor, level: 21, variant: "FULLSONG")
            },
            new[] { new ExternalChartAlias(rejected, chart.Id, ExternalAliasStatus.Auto, Now.AddMonths(-2)) });

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
                list.Single().ExternalKey == survivor && list.Single().ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     We store the localized artist alongside the Latin one and piucenter carries Latin only.
    ///     Normalization cannot bridge that on its own: Hangul characters ARE letters, so they
    ///     survive the fold and "IVE (아이브)" never meets "IVE".
    /// </summary>
    [Fact]
    public async Task AnArtistsLocalizedParentheticalDoesNotBlockTheMatch()
    {
        var chart = new ChartBuilder().WithSongName("BANG BANG").WithArtist("IVE (아이브)").WithLevel(15).Build();
        var key = "BANG_BANG_-_IVE_S15_ARCADE";
        SetupDefaults(new[] { chart }, new[] { Listing(key) }, Array.Empty<ExternalChartAlias>());

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
            list.Single().ExternalKey == key && list.Single().ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The relaxed artist is a FALLBACK, never a rewrite: where an exact artist claims the
    ///     key, a stripped one must not be able to take it.
    /// </summary>
    [Fact]
    public async Task AnExactArtistBeatsTheStrippedFallback()
    {
        var exact = new ChartBuilder().WithSongName("Cover Me").WithArtist("AKB48").WithLevel(15).Build();
        var qualified = new ChartBuilder().WithSongName("Cover Me").WithArtist("AKB48 (Cover)").WithLevel(15)
            .Build();
        var key = "Cover_Me_-_AKB48_S15_ARCADE";
        SetupDefaults(new[] { qualified, exact }, new[] { Listing(key) }, Array.Empty<ExternalChartAlias>());

        await Consume();

        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
            list.Single().ExternalKey == key && list.Single().ChartId == exact.Id),
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
            new[] { "run" }, true, 12.0m, "12th notes @ 240 bpm", "D20",
            TapRows: 577, HoldRows: 70, HoldTickSum: 481);
        SetupDefaults(new[] { chart }, new[] { Listing(key, ChartType.Double, 20) },
            new[] { new ExternalChartAlias(key, chart.Id, ExternalAliasStatus.Auto, Now) }, page: page);

        await Consume();

        _metrics.Verify(m => m.ReplaceChartMetrics(PiuCenterMetrics.Source,
            It.Is<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>>(byChart =>
                byChart[chart.Id].Any(r => r.MetricName == PiuCenterMetrics.DataVersion && r.Value == 50726m) &&
                byChart[chart.Id].Any(r => r.MetricName == "top3:bracket_drill" && r.Value == 1m) &&
                byChart[chart.Id].Any(r => r.MetricName == "badge_fraction:twist_90" && r.Value == 0.5m) &&
                byChart[chart.Id].Any(r => r.MetricName == "last_segment_badge:run") &&
                byChart[chart.Id].Any(r => r.MetricName == PiuCenterMetrics.TapRows && r.Value == 577m) &&
                byChart[chart.Id].Any(r => r.MetricName == PiuCenterMetrics.HoldRows && r.Value == 70m) &&
                byChart[chart.Id].Any(r => r.MetricName == PiuCenterMetrics.HoldTicks && r.Value == 481m)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The one-write rule (owner, 2026-08-30). Banking per chart evicted the whole-source
    ///     metric cache once per chart — ~4,500 times per snapshot upload — and every live read
    ///     between two writes re-hydrated the full table just to have the next write throw it
    ///     away. However many pages a crawl fetches, the metrics land as ONE replace.
    /// </summary>
    [Fact]
    public async Task ACrawlBanksEveryFetchedChartInOneWrite()
    {
        var first = new ChartBuilder().WithSongName("Repentance").WithLevel(20).Build();
        var second = new ChartBuilder().WithSongName("Achluoias").WithLevel(24).Build();
        var page = new PiuCenterChartPage("x", new[] { "run" }, 8,
            new Dictionary<string, int> { ["run"] = 4 },
            new Dictionary<string, int>(),
            new[] { "run" }, true, 12.0m, "12th notes @ 240 bpm", "D20",
            TapRows: 577, HoldRows: 70, HoldTickSum: 481);
        SetupDefaults(new[] { first, second },
            new[]
            {
                Listing("Repentance_-_Abel_D20_ARCADE", ChartType.Single, 20),
                Listing("Achluoias_-_Abel_D24_ARCADE", ChartType.Single, 24)
            },
            new[]
            {
                new ExternalChartAlias("Repentance_-_Abel_D20_ARCADE", first.Id, ExternalAliasStatus.Auto, Now),
                new ExternalChartAlias("Achluoias_-_Abel_D24_ARCADE", second.Id, ExternalAliasStatus.Auto, Now)
            }, page: page);

        await Consume();

        _metrics.Verify(m => m.ReplaceChartMetrics(PiuCenterMetrics.Source,
            It.Is<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>>(byChart =>
                byChart.Count == 2 && byChart.ContainsKey(first.Id) && byChart.ContainsKey(second.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.ReplaceChartMetrics(It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>>(),
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
        // alias reconcile, metric banking (stamped with the zip's version so the weekly
        // crawl stays a no-op), baseline rebuild — with the client never touched.
        var chart = new ChartBuilder().WithSongName("Slam").WithArtist("Novasonic").WithLevel(7).Build();
        var key = "Slam_-_Novasonic_S7_ARCADE";
        var storage = new List<ChartSkillMetric>();
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _aliases.Setup(a => a.GetAliases(PiuCenterMetrics.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExternalChartAlias>());
        _metrics.Setup(m => m.ReplaceChartMetrics(PiuCenterMetrics.Source,
                It.IsAny<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>> byChart,
                    CancellationToken _) =>
                storage.AddRange(byChart.SelectMany(kv => kv.Value)))
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

        // Auto-matched from the zip's table and banked with the zip's version.
        _aliases.Verify(a => a.SaveAliases(PiuCenterMetrics.Source, It.Is<IEnumerable<ExternalChartAlias>>(list =>
                list.Single().ExternalKey == key && list.Single().ChartId == chart.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(storage, r => r.MetricName == PiuCenterMetrics.DataVersion && r.Value == 50726m);
        Assert.Contains(storage, r => r.MetricName == "badge_fraction:twist_90" && r.Value == 1m);
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
}
