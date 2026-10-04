using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
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

    /// <summary>
    ///     A mix's row exists only because a migration seeded it: nothing else writes one, the local
    ///     populate harness included, and the catalog's per-mix rows (a chart in a mix, a mix's
    ///     versions, a song's channel in a mix) name it by a foreign key. A mix value without its
    ///     seed row would surface as a key violation the first time a fresh database took that
    ///     mix's charts.
    /// </summary>
    [Fact]
    public void Migrations_seed_a_mix_row_for_every_mix()
    {
        var seeded = _fixture.MigratedMixes.Select(m => m.Id).ToHashSet();

        var unseeded = Enum.GetValues<MixEnum>().Where(mix => !seeded.Contains(MixIds.For(mix)));

        Assert.Empty(unseeded);
    }
}
