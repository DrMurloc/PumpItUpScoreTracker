using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Crawl metrics are cached whole per source and sliced in memory, so the interesting
///     behaviour is no longer the query — it is the cache: that a slice returns only what was
///     asked for, and that a write is seen. Nothing covered this repository before it grew a
///     cache; a stale metric set would have shown up as a chart silently keeping its old
///     difficulty profile forever.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFChartSkillMetricRepositoryTests : IAsyncLifetime
{
    private const string Source = "piucenter";
    private const string OtherSource = "someone-else";

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFChartSkillMetricRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    ///     One shared cache across the repositories a test builds — that is the production
    ///     shape (a singleton IMemoryCache), and it is the only way an eviction bug can show.
    /// </summary>
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private EFChartSkillMetricRepository BuildRepository() => new(_cache, _fixture.DbContextFactory);

    private static ChartSkillMetric Metric(Guid chartId, string name, decimal value) =>
        new(chartId, name, value, null);

    [Fact]
    public async Task GetMetricsReturnsOnlyTheChartsAskedFor()
    {
        // The whole source is cached; the slice is done in memory. A slice that leaked its
        // neighbours would hand the similarity job another chart's badge profile.
        var wanted = await _seed.SeedChartAsync();
        var other = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(wanted, Source,
            new[] { Metric(wanted, "nps", 10.7m), Metric(wanted, "badge_fraction:bracket", 0.5m) },
            CancellationToken.None);
        await repository.ReplaceChartMetrics(other, Source,
            new[] { Metric(other, "nps", 3.1m) }, CancellationToken.None);

        var result = await repository.GetMetrics(new[] { wanted }, Source, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal(wanted, m.ChartId));
        Assert.Equal(10.7m, result.Single(m => m.MetricName == "nps").Value);
    }

    [Fact]
    public async Task AWriteIsVisibleToTheNextRead()
    {
        // The cache holds for 14 days, so the expiry is not the mechanism — ReplaceChartMetrics
        // evicting is. Without that, a re-crawl would be invisible until a restart.
        var chart = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(chart, Source, new[] { Metric(chart, "nps", 8.0m) },
            CancellationToken.None);
        Assert.Equal(8.0m, (await repository.GetMetrics(new[] { chart }, Source, CancellationToken.None))
            .Single().Value);

        // A re-crawl of the same chart, through a different instance sharing the cache.
        await BuildRepository().ReplaceChartMetrics(chart, Source, new[] { Metric(chart, "nps", 12.5m) },
            CancellationToken.None);

        var result = await repository.GetMetrics(new[] { chart }, Source, CancellationToken.None);
        Assert.Equal(12.5m, Assert.Single(result).Value);
    }

    [Fact]
    public async Task SourcesAreCachedApart()
    {
        var chart = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(chart, Source, new[] { Metric(chart, "nps", 10.0m) },
            CancellationToken.None);
        await repository.ReplaceChartMetrics(chart, OtherSource, new[] { Metric(chart, "nps", 99.0m) },
            CancellationToken.None);

        Assert.Equal(10.0m, (await repository.GetMetrics(new[] { chart }, Source, CancellationToken.None))
            .Single().Value);
        Assert.Equal(99.0m, (await repository.GetMetrics(new[] { chart }, OtherSource, CancellationToken.None))
            .Single().Value);
    }

    [Fact]
    public async Task ReplacingWithNothingClearsTheChart()
    {
        // A crawl that finds a chart has no analysis must not leave the old rows standing.
        var chart = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(chart, Source, new[] { Metric(chart, "nps", 8.0m) },
            CancellationToken.None);

        await repository.ReplaceChartMetrics(chart, Source, Array.Empty<ChartSkillMetric>(),
            CancellationToken.None);

        Assert.Empty(await repository.GetMetrics(new[] { chart }, Source, CancellationToken.None));
        Assert.DoesNotContain(chart, await repository.GetChartIdsWithMetrics(Source, CancellationToken.None));
    }

    [Fact]
    public async Task GetChartIdsWithMetricsSeesWritesAndIsSourceScoped()
    {
        var mine = await _seed.SeedChartAsync();
        var theirs = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(mine, Source, new[] { Metric(mine, "nps", 8.0m) },
            CancellationToken.None);
        await repository.ReplaceChartMetrics(theirs, OtherSource, new[] { Metric(theirs, "nps", 8.0m) },
            CancellationToken.None);

        var ids = await repository.GetChartIdsWithMetrics(Source, CancellationToken.None);

        Assert.Contains(mine, ids);
        Assert.DoesNotContain(theirs, ids);
    }

    [Fact]
    public async Task AChartWithNoMetricsIsAbsentRatherThanAnError()
    {
        var chart = await _seed.SeedChartAsync();

        Assert.Empty(await BuildRepository().GetMetrics(new[] { chart }, Source, CancellationToken.None));
    }

    [Fact]
    public async Task GetMetricsByChartReturnsTheWholeSourceKeyedByChart()
    {
        // The SRP search reads every chart's badges and NPS in one go — the whole-source
        // dictionary, source-scoped, so another crawler's rows never bleed into the facet.
        var first = await _seed.SeedChartAsync();
        var second = await _seed.SeedChartAsync();
        var foreign = await _seed.SeedChartAsync();
        var repository = BuildRepository();
        await repository.ReplaceChartMetrics(first, Source,
            new[] { Metric(first, "top3:drill", 1m), Metric(first, "nps", 11.2m) }, CancellationToken.None);
        await repository.ReplaceChartMetrics(second, Source,
            new[] { Metric(second, "top3:twist_over90", 1m) }, CancellationToken.None);
        await repository.ReplaceChartMetrics(foreign, OtherSource,
            new[] { Metric(foreign, "nps", 9.9m) }, CancellationToken.None);

        var byChart = await repository.GetMetricsByChart(Source, CancellationToken.None);

        Assert.Equal(2, byChart.Count);
        Assert.Equal(11.2m, byChart[first].Single(m => m.MetricName == "nps").Value);
        Assert.Equal("top3:twist_over90", Assert.Single(byChart[second]).MetricName);
        Assert.DoesNotContain(foreign, byChart.Keys);
    }
}
