using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The deep-scan balance against a real database. The spend has to be atomic — a
///     read-then-write would hand two tabs the same last scan — and only SQL can prove that.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class DeepScanAllowanceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public DeepScanAllowanceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private EFUserRepository Users()
    {
        return new EFUserRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task ANewAccountStartsWithTheAllowanceTheMigrationDefaults()
    {
        var userId = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();

        // A zero default would silently deny every existing player their scans until the reset.
        Assert.Equal(3, await Users().GetDeepScansRemaining(userId, CancellationToken.None));
    }

    [Fact]
    public async Task SpendingDrawsTheBalanceDownAndStopsAtZero()
    {
        var users = Users();
        var userId = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();

        Assert.True(await users.TrySpendDeepScan(userId, CancellationToken.None));
        Assert.True(await users.TrySpendDeepScan(userId, CancellationToken.None));
        Assert.True(await users.TrySpendDeepScan(userId, CancellationToken.None));
        Assert.False(await users.TrySpendDeepScan(userId, CancellationToken.None));

        // Refused, not driven negative — the guard is in the WHERE clause.
        Assert.Equal(0, await users.GetDeepScansRemaining(userId, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentSpendsNeverOversellTheBalance()
    {
        var users = Users();
        var userId = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();

        // Eight racing requests against a balance of three: exactly three may win. A
        // read-then-write repository passes every other test in this file and fails this one.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Users().TrySpendDeepScan(userId, CancellationToken.None)));

        Assert.Equal(3, attempts.Count(granted => granted));
        Assert.Equal(0, await users.GetDeepScansRemaining(userId, CancellationToken.None));
    }

    [Fact]
    public async Task TheResetRefillsEveryAccountAndDoesNotRollOver()
    {
        var users = Users();
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var spender = await seeder.SeedUserAsync();
        var untouched = await seeder.SeedUserAsync();
        await users.TrySpendDeepScan(spender, CancellationToken.None);
        await users.TrySpendDeepScan(spender, CancellationToken.None);

        await users.ResetDeepScans(3, CancellationToken.None);

        Assert.Equal(3, await users.GetDeepScansRemaining(spender, CancellationToken.None));
        // Set, not incremented: an unused month does not bank scans for the next one.
        Assert.Equal(3, await users.GetDeepScansRemaining(untouched, CancellationToken.None));
    }

    [Fact]
    public async Task AHandGrantedBalanceSurvivesUntilTheNextReset()
    {
        var users = Users();
        var userId = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();

        // The support lever: one UPDATE gives a player more scans.
        await users.ResetDeepScans(3, CancellationToken.None);
        await using (var database = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            var row = database.User.Single(u => u.Id == userId);
            row.DeepScansRemaining = 10;
            await database.SaveChangesAsync();
        }

        for (var i = 0; i < 10; i++)
            Assert.True(await users.TrySpendDeepScan(userId, CancellationToken.None));
        Assert.False(await users.TrySpendDeepScan(userId, CancellationToken.None));
    }
}
