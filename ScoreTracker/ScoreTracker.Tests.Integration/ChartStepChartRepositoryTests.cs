using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartStepChartRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public ChartStepChartRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFChartStepChartRepository BuildRepository() =>
        new(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory);

    [Fact]
    public async Task ReplaceBanksAndGetReadsBack()
    {
        var repo = BuildRepository();
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();

        await repo.Replace(new Dictionary<Guid, BankedStepChart>
        {
            [chartA] = new("82626", Now, new byte[] { 1, 2, 3 }),
            [chartB] = new("82626", Now, new byte[] { 4, 5 })
        });

        var banked = await repo.Get(chartA);
        Assert.NotNull(banked);
        Assert.Equal("82626", banked!.Vintage);
        Assert.Equal(new byte[] { 1, 2, 3 }, banked.Payload);
    }

    [Fact]
    public async Task ANewVintageOverwritesAndTheCacheTurnsOver()
    {
        var repo = BuildRepository();
        var chartId = Guid.NewGuid();
        await repo.Replace(new Dictionary<Guid, BankedStepChart>
        {
            [chartId] = new("50726", Now.AddDays(-30), new byte[] { 1 })
        });
        // Prime the cache with the old row, then replace through the same repository.
        Assert.Equal("50726", (await repo.Get(chartId))!.Vintage);

        await repo.Replace(new Dictionary<Guid, BankedStepChart>
        {
            [chartId] = new("82626", Now, new byte[] { 2, 2 })
        });

        var banked = await repo.Get(chartId);
        Assert.Equal("82626", banked!.Vintage);
        Assert.Equal(new byte[] { 2, 2 }, banked.Payload);
    }

    [Fact]
    public async Task AChartNeverBankedReadsAsNothing()
    {
        Assert.Null(await BuildRepository().Get(Guid.NewGuid()));
    }
}
