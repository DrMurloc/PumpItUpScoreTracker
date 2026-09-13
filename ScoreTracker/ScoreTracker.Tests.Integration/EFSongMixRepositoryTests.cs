using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     A song's channel per mix against a real database: the one write inserts or overwrites the
///     (song, mix) row, and the per-mix chart dictionary carries what it wrote — null where the
///     mix has no row for the song (docs/design/song-channels.md §3).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFSongMixRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFSongMixRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetChannelInsertsTheRowForTheSongOnThatMix()
    {
        var chartId = await _seed.SeedPhoenixChartAsync();
        var songId = await SongOf(chartId);
        var cache = new MemoryCache(new MemoryCacheOptions());

        await new EFSongMixRepository(cache, _fixture.DbContextFactory).SetChannel(MixEnum.Phoenix, songId, Channel.KPop);

        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        var rows = await db.Set<SongMixEntity>().Where(r => r.SongId == songId).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(TestDataSeeder.PhoenixMixId, row.MixId);
        Assert.Equal("KPop", row.Channel);
    }

    [Fact]
    public async Task SetChannelOverwritesRatherThanDuplicating()
    {
        var chartId = await _seed.SeedPhoenixChartAsync();
        var songId = await SongOf(chartId);
        var repository = new EFSongMixRepository(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory);

        await repository.SetChannel(MixEnum.Phoenix, songId, Channel.JMusic);
        await repository.SetChannel(MixEnum.Phoenix, songId, Channel.WorldMusic);

        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        var row = Assert.Single(await db.Set<SongMixEntity>().Where(r => r.SongId == songId).ToListAsync());
        Assert.Equal("WorldMusic", row.Channel);
    }

    [Fact]
    public async Task TheChartDictionaryCarriesTheChannelOfTheMixInView()
    {
        var chartId = await _seed.SeedPhoenixChartAsync();
        var songId = await SongOf(chartId);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var charts = new EFChartRepository(cache, _fixture.DbContextFactory);
        var songMixes = new EFSongMixRepository(cache, _fixture.DbContextFactory);

        // Read first, so the write has a cached dictionary to evict.
        Assert.Null((await charts.GetChart(MixEnum.Phoenix, chartId)).Song.Channel);

        await songMixes.SetChannel(MixEnum.Phoenix, songId, Channel.Xross);

        Assert.Equal(Channel.Xross, (await charts.GetChart(MixEnum.Phoenix, chartId)).Song.Channel);
    }

    [Fact]
    public async Task AMixWithNoRowForTheSongReadsNoChannel()
    {
        await _seed.EnsureMixAsync(TestDataSeeder.Phoenix2MixId);
        var chartId = await _seed.SeedPhoenixChartAsync();
        var songId = await SongOf(chartId);
        await using (var db = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            db.ChartMix.Add(new Data.Persistence.Entities.ChartMixEntity
                { Id = Guid.NewGuid(), ChartId = chartId, MixId = TestDataSeeder.Phoenix2MixId, Level = 15 });
            await db.SaveChangesAsync();
        }

        var cache = new MemoryCache(new MemoryCacheOptions());
        await new EFSongMixRepository(cache, _fixture.DbContextFactory).SetChannel(MixEnum.Phoenix, songId, Channel.JMusic);
        var charts = new EFChartRepository(cache, _fixture.DbContextFactory);

        Assert.Equal(Channel.JMusic, (await charts.GetChart(MixEnum.Phoenix, chartId)).Song.Channel);
        Assert.Null((await charts.GetChart(MixEnum.Phoenix2, chartId)).Song.Channel);
    }

    private async Task<Guid> SongOf(Guid chartId)
    {
        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await db.Chart.Where(c => c.Id == chartId).Select(c => c.SongId).SingleAsync();
    }
}
