using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Data.Repositories;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
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
    public async Task CreateSongPersistsTheKoreanCultureNameRow()
    {
        // The bulk-add and single-song admin flows both rely on CreateSong writing the ko-KR
        // row itself — if it stops persisting, every new song is invisible to Korean imports.
        await BuildRepository().CreateSong("Nacho Beach", "나쵸 비치",
            new Uri("https://piuimages.arroweclip.se/songs/NachoBeach.png"), SongType.Arcade,
            TimeSpan.FromSeconds(105), "Doin", Bpm.From(195, 195));

        var koreanToEnglish = await BuildRepository().GetEnglishLookup("ko-KR", CancellationToken.None);

        Assert.Equal("Nacho Beach", (string)koreanToEnglish["나쵸 비치"]);
    }

    [Fact]
    public async Task CreateChartStoresThePlayerCountForCoOpCharts()
    {
        // Co-ops store the player count in Level, and readers treat the persisted
        // PlayerCount column as authoritative (the domain fallback never fires for
        // DB-loaded charts) — a co-op left at the column default of 1 renders
        // "CoOp x1" and buckets under the wrong player count.
        await _seed.EnsurePhoenixMixAsync();
        var writer = BuildRepository();
        var songId = await writer.CreateSong("Team Up", "팀 업",
            new Uri("https://piuimages.arroweclip.se/songs/TeamUp.png"), SongType.Arcade,
            TimeSpan.FromSeconds(100), "Doin", Bpm.From(150, 150));
        var coOpId = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.CoOp, 2,
            "PUMP IT UP Official", new Uri("https://www.youtube.com/embed/aaaaaaaaaaa"), "SUNNY");
        var singleId = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 22,
            "PUMP IT UP Official", new Uri("https://www.youtube.com/embed/bbbbbbbbbbb"), "SUNNY");

        var charts = (await BuildRepository().GetCharts(MixEnum.Phoenix)).ToDictionary(c => c.Id);

        Assert.Equal(2, charts[coOpId].PlayerCount);
        Assert.Equal(1, charts[singleId].PlayerCount);
    }

    // --- Video sides (docs/design/video-sides.md) ---
    // Sharing a URL between two of a song's singles IS the registration: the write paths
    // recompute sides themselves, so the admin flows need no pairing support of their own.

    private async Task<Guid> CreateSongAsync(EFChartRepository writer, string name)
    {
        return await writer.CreateSong(name, name,
            new Uri("https://piuimages.arroweclip.se/songs/VideoSides.png"), SongType.Arcade,
            TimeSpan.FromSeconds(100), "Doin", Bpm.From(150, 150));
    }

    [Fact]
    public async Task CreateChartRegistersSidesWhenTwoSinglesOfOneSongShareAVideo()
    {
        await _seed.EnsurePhoenixMixAsync();
        var writer = BuildRepository();
        var songId = await CreateSongAsync(writer, "Uh-Heung");
        var shared = new Uri("https://www.youtube.com/embed/ccccccccccc");
        var lower = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 17,
            "NEVSISTER", shared, "EXC");
        var higher = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 22,
            "NEVSISTER", shared, "EXC");

        var videos = (await BuildRepository().GetChartVideoInformation(new[] { lower, higher }))
            .ToDictionary(v => v.ChartId);

        Assert.Equal(VideoSide.Left, videos[lower].Side);
        Assert.Equal(VideoSide.Right, videos[higher].Side);
        Assert.Equal(higher, videos[lower].PartnerChartId);
        Assert.Equal(lower, videos[higher].PartnerChartId);
    }

    [Fact]
    public async Task SetChartVideoMovingOneChartOffASharedVideoClearsTheStrandedPartnerToo()
    {
        await _seed.EnsurePhoenixMixAsync();
        var writer = BuildRepository();
        var songId = await CreateSongAsync(writer, "Uh-Heung");
        var shared = new Uri("https://www.youtube.com/embed/ccccccccccc");
        var lower = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 17,
            "NEVSISTER", shared, "EXC");
        var higher = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 22,
            "NEVSISTER", shared, "EXC");

        await writer.SetChartVideo(higher, new Uri("https://www.youtube.com/embed/ddddddddddd"),
            "NEVSISTER");

        var videos = (await BuildRepository().GetChartVideoInformation(new[] { lower, higher }))
            .ToDictionary(v => v.ChartId);
        Assert.Null(videos[lower].Side);
        Assert.Null(videos[higher].Side);
        Assert.Null(videos[lower].PartnerChartId);
        Assert.Null(videos[higher].PartnerChartId);
    }

    [Fact]
    public async Task ChartsOfDifferentSongsSharingAVideoGetNoSides()
    {
        // The cross-song mislink shape: each song sees a solo row and the URL's total row count
        // says it is shared, so both stay sideless rather than pairing across songs.
        await _seed.EnsurePhoenixMixAsync();
        var writer = BuildRepository();
        var shared = new Uri("https://www.youtube.com/embed/eeeeeeeeeee");
        var songA = await CreateSongAsync(writer, "PICK ME");
        var songB = await CreateSongAsync(writer, "Nekkoya");
        var chartA = await writer.CreateChart(MixEnum.Phoenix, songA, ChartType.Single, 4,
            "NEVSISTER", shared, "EXC");
        var chartB = await writer.CreateChart(MixEnum.Phoenix, songB, ChartType.Single, 6,
            "NEVSISTER", shared, "EXC");

        var videos = (await BuildRepository().GetChartVideoInformation(new[] { chartA, chartB }))
            .ToDictionary(v => v.ChartId);

        Assert.Null(videos[chartA].Side);
        Assert.Null(videos[chartB].Side);
    }

    [Fact]
    public async Task RecomputePreservesHandResearchedSidesOnASinglePlusPerformancePair()
    {
        // S+SP sides come from watching the video, not from levels — a later write to the same
        // song must leave them exactly as set.
        await _seed.EnsurePhoenixMixAsync();
        var writer = BuildRepository();
        var songId = await CreateSongAsync(writer, "Come to Me");
        var shared = new Uri("https://www.youtube.com/embed/fffffffffff");
        var single = await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Single, 4,
            "NEVSISTER", shared, "EXC");
        var performance = await writer.CreateChart(MixEnum.Phoenix, songId,
            ChartType.SinglePerformance, 3, "NEVSISTER", shared, "EXC");

        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            var rows = await ctx.Set<ScoreTracker.Catalog.Infrastructure.Entities.ChartVideoEntity>()
                .Where(v => v.ChartId == single || v.ChartId == performance)
                .ToArrayAsync();
            foreach (var row in rows)
                row.Side = row.ChartId == performance ? "Left" : "Right";
            await ctx.SaveChangesAsync();
        }

        // Any same-song write triggers the recompute; the S+SP pair must come through untouched.
        await writer.CreateChart(MixEnum.Phoenix, songId, ChartType.Double, 20, "NEVSISTER",
            new Uri("https://www.youtube.com/embed/ggggggggggg"), "EXC");

        var videos = (await BuildRepository().GetChartVideoInformation(new[] { single, performance }))
            .ToDictionary(v => v.ChartId);
        Assert.Equal(VideoSide.Left, videos[performance].Side);
        Assert.Equal(VideoSide.Right, videos[single].Side);
        Assert.Equal(single, videos[performance].PartnerChartId);
        Assert.Equal(performance, videos[single].PartnerChartId);
    }

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
