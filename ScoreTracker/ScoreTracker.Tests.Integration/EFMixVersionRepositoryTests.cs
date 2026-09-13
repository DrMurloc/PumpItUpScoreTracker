using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The mix's patches against a real database: the read is the whole ordered list, the one
///     write appends at the end of the order and never mints a second row for a name the mix
///     already has (docs/design/chart-versions.md §2, §6).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFMixVersionRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public EFMixVersionRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // The list is cached per mix for a fortnight, so each fact builds its own repository to
    // read what it seeded rather than what an earlier fact left in the cache.
    private EFMixVersionRepository BuildRepository() =>
        new(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory);

    [Fact]
    public async Task GetVersionsReturnsTheMixesPatchesInReleaseOrder()
    {
        await _seed.SeedMixVersionAsync(TestDataSeeder.PhoenixMixId, "2.00.0", new DateOnly(2024, 5, 27), 100);
        await _seed.SeedMixVersionAsync(TestDataSeeder.PhoenixMixId, "1.00.0", new DateOnly(2023, 7, 4), 10);
        await _seed.SeedMixVersionAsync(TestDataSeeder.PhoenixMixId, "1.01.0", null, 20);

        var versions = await BuildRepository().GetVersions(MixEnum.Phoenix);

        Assert.Equal(new[] { "1.00.0", "1.01.0", "2.00.0" }, versions.Select(v => v.Name));
        Assert.Equal(new DateOnly(2023, 7, 4), versions[0].ReleaseDate);
        Assert.Null(versions[1].ReleaseDate);
        Assert.All(versions, v => Assert.Equal(MixEnum.Phoenix, v.Mix));
    }

    [Fact]
    public async Task GetVersionsReadsOneMixOnly()
    {
        await _seed.SeedMixVersionAsync(TestDataSeeder.PhoenixMixId, "1.00.0", new DateOnly(2023, 7, 4), 10);
        await _seed.SeedMixVersionAsync(TestDataSeeder.Phoenix2MixId, "1.00.0", new DateOnly(2026, 7, 9), 10);
        await _seed.SeedMixVersionAsync(TestDataSeeder.Phoenix2MixId, "1.01.0", new DateOnly(2026, 9, 3), 20);

        var phoenix2 = await BuildRepository().GetVersions(MixEnum.Phoenix2);

        Assert.Equal(new[] { "1.00.0", "1.01.0" }, phoenix2.Select(v => v.Name));
        Assert.Equal(new DateOnly(2026, 7, 9), phoenix2[0].ReleaseDate);
    }

    [Fact]
    public async Task CreateAppendsAtTheEndOfTheOrder()
    {
        await _seed.SeedMixVersionAsync(TestDataSeeder.Phoenix2MixId, "1.00.0", new DateOnly(2026, 7, 9), 10);

        var id = await BuildRepository().Create(MixEnum.Phoenix2, " 1.01.0 ", new DateOnly(2026, 9, 3));

        var versions = await BuildRepository().GetVersions(MixEnum.Phoenix2);
        Assert.Equal(new[] { "1.00.0", "1.01.0" }, versions.Select(v => v.Name));
        Assert.Equal(id, versions[1].Id);
        Assert.Equal(20, versions[1].SortOrder);
        Assert.Equal(new DateOnly(2026, 9, 3), versions[1].ReleaseDate);
    }

    [Fact]
    public async Task CreateOnAnEmptyMixStartsTheOrder()
    {
        await _seed.EnsureMixAsync(TestDataSeeder.Phoenix2MixId);

        await BuildRepository().Create(MixEnum.Phoenix2, "1.00.0", null);

        var versions = await BuildRepository().GetVersions(MixEnum.Phoenix2);
        Assert.Single(versions);
        Assert.Equal(10, versions[0].SortOrder);
    }

    [Fact]
    public async Task CreateReturnsTheExistingRowForANameTheMixAlreadyHas()
    {
        var existing = await _seed.SeedMixVersionAsync(TestDataSeeder.Phoenix2MixId, "1.01.0", new DateOnly(2026, 9, 3), 20);

        var id = await BuildRepository().Create(MixEnum.Phoenix2, "1.01.0", new DateOnly(2026, 9, 4));

        Assert.Equal(existing, id);
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.Set<MixVersionEntity>().CountAsync(v => v.MixId == TestDataSeeder.Phoenix2MixId));
    }

    [Fact]
    public async Task CreateEvictsTheCachedList()
    {
        await _seed.SeedMixVersionAsync(TestDataSeeder.Phoenix2MixId, "1.00.0", new DateOnly(2026, 7, 9), 10);
        var repository = BuildRepository();
        Assert.Single(await repository.GetVersions(MixEnum.Phoenix2));

        await repository.Create(MixEnum.Phoenix2, "1.01.0", new DateOnly(2026, 9, 3));

        Assert.Equal(2, (await repository.GetVersions(MixEnum.Phoenix2)).Count);
    }
}
