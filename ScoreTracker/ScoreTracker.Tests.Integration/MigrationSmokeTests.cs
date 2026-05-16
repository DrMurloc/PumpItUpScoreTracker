using Microsoft.EntityFrameworkCore;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class MigrationSmokeTests
{
    private readonly SqlServerFixture _fixture;

    public MigrationSmokeTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_have_applied_and_no_pending_remain()
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }
}
