using ScoreTracker.Catalog.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Repositories;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFChartRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFChartRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // `GetAllCharts` caches the full per-mix chart set for 14 days, so writes are invisible to
    // an existing repo instance. Building a fresh repo per call guarantees a cache miss.
    private EFChartRepository BuildRepository() =>
        new(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory);

    [Fact]
    public async Task GetChartsReturnsOnlyChartsTaggedInTheRequestedMix()
    {
        var inPhoenix = await _seed.SeedPhoenixChartAsync();
        var notInPhoenix = await _seed.SeedChartAsync();

        var charts = (await BuildRepository().GetCharts(MixEnum.Phoenix)).ToList();

        Assert.Contains(charts, c => c.Id == inPhoenix);
        Assert.DoesNotContain(charts, c => c.Id == notInPhoenix);
    }

    [Fact]
    public async Task GetChartsFiltersByLevel()
    {
        await _seed.SeedPhoenixChartAsync(level: 14);
        var l15 = await _seed.SeedPhoenixChartAsync(level: 15);
        await _seed.SeedPhoenixChartAsync(level: 16);

        var charts = (await BuildRepository().GetCharts(MixEnum.Phoenix, level: 15)).ToList();

        Assert.Single(charts);
        Assert.Equal(l15, charts[0].Id);
    }

    [Fact]
    public async Task GetChartsFiltersByChartType()
    {
        var single = await _seed.SeedPhoenixChartAsync(type: "Single");
        var doublesy = await _seed.SeedPhoenixChartAsync(type: "Double");

        var singles = (await BuildRepository().GetCharts(MixEnum.Phoenix, type: ChartType.Single)).ToList();

        Assert.Contains(singles, c => c.Id == single);
        Assert.DoesNotContain(singles, c => c.Id == doublesy);
    }

    [Fact]
    public async Task GetChartsFiltersByChartIds()
    {
        var wanted = await _seed.SeedPhoenixChartAsync();
        var unwanted = await _seed.SeedPhoenixChartAsync();

        var charts = (await BuildRepository()
            .GetCharts(MixEnum.Phoenix, chartIds: new[] { wanted })).ToList();

        Assert.Single(charts);
        Assert.Equal(wanted, charts[0].Id);
    }

    [Fact]
    public async Task GetSongNamesReturnsOneEntryPerDistinctSongInTheMix()
    {
        // Each seeded chart creates its own song, so two Phoenix charts → two song names.
        // A non-Phoenix chart should not appear.
        await _seed.SeedPhoenixChartAsync();
        await _seed.SeedPhoenixChartAsync();
        await _seed.SeedChartAsync();

        var names = (await BuildRepository().GetSongNames(MixEnum.Phoenix)).ToList();

        Assert.Equal(2, names.Count);
    }

    // --- Korean name lookup ---
    // The score-import path receives Korean song names from PIU when the scraper picks up a
    // Korean session. `OfficialSiteClient.GetMappedName` resolves them via these methods; if
    // any of them ever stops mapping Korean → English correctly, every Korean user's score
    // import silently fails to match a chart. There aren't enough Korean users in production
    // to catch regressions organically, so these tests stand in.

    [Fact]
    public async Task SetSongCultureNameThenGetEnglishLookupResolvesKoreanToEnglish()
    {
        var writer = BuildRepository();
        await writer.SetSongCultureName("TRICKL4SH 220", "ko-KR", "트릭크래쉬 220");
        await writer.SetSongCultureName("Appassionata", "ko-KR", "열정");

        var koreanToEnglish = await BuildRepository().GetEnglishLookup("ko-KR", CancellationToken.None);

        Assert.Equal("TRICKL4SH 220", (string)koreanToEnglish["트릭크래쉬 220"]);
        Assert.Equal("Appassionata", (string)koreanToEnglish["열정"]);
    }

    [Fact]
    public async Task SetSongCultureNameThenGetSongNamesReturnsEnglishToKoreanMapping()
    {
        var writer = BuildRepository();
        await writer.SetSongCultureName("TRICKL4SH 220", "ko-KR", "트릭크래쉬 220");

        var englishToKorean = await BuildRepository().GetSongNames("ko-KR", CancellationToken.None);

        Assert.Equal("트릭크래쉬 220", (string)englishToKorean["TRICKL4SH 220"]);
    }

    [Fact]
    public async Task SetSongCultureNameUpdatesExistingMappingRatherThanDuplicating()
    {
        // If a Korean translation gets revised, calling SetSongCultureName again should overwrite
        // the existing row, not insert a duplicate (which would leak the stale name into the
        // English-lookup dict and break import for anyone with the old name cached).
        var writer = BuildRepository();
        await writer.SetSongCultureName("TRICKL4SH 220", "ko-KR", "오래된 번역");
        await writer.SetSongCultureName("TRICKL4SH 220", "ko-KR", "트릭크래쉬 220");

        var koreanToEnglish = await BuildRepository().GetEnglishLookup("ko-KR", CancellationToken.None);
        var englishToKorean = await BuildRepository().GetSongNames("ko-KR", CancellationToken.None);

        Assert.False(koreanToEnglish.ContainsKey("오래된 번역"));
        Assert.Equal("TRICKL4SH 220", (string)koreanToEnglish["트릭크래쉬 220"]);
        Assert.Equal("트릭크래쉬 220", (string)englishToKorean["TRICKL4SH 220"]);
    }
}
