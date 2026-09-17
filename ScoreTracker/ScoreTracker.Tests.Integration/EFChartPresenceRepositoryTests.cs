using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The PUMBILITY presence census's storage against a real database
///     (docs/design/chart-presence-graph.md §7): every field a row carries survives the round trip, and a
///     census replaces its own mix whole without touching another.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFChartPresenceRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset ComputedAt = new(2026, 9, 16, 12, 30, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFChartPresenceRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFChartPresenceRepository Repository() => new(_fixture.DbContextFactory);

    [Fact]
    public async Task ACensusRoundTripsItsColumnsAndEveryFieldOfItsRows()
    {
        var chart = await new TestDataSeeder(_fixture.DbContextFactory).SeedChartAsync(22);
        var census = new ChartPresenceCensusResult(
            new[]
            {
                new ChartPresenceColumnRow(true, 0, Name.From("[P.B] BRONZE"), 23, 23),
                new ChartPresenceColumnRow(true, 1, Name.From("[P.B] DIAMOND LV.1"), 27, 25),
                // The same place in the other layout, counted over PIU Scores accounts alone.
                new ChartPresenceColumnRow(false, 0, Name.From("[P.B] BRONZE"), 23, 23),
                new ChartPresenceColumnRow(false, 1, Name.From("[P.B] DIAMOND"), 95, 95)
            },
            new[]
            {
                new ChartPresenceRow(chart, 0, false, 3, new PumbilitySpots(1.5, 2, 3, 7.25, 12), new[] { 1.5, 3, 12 },
                    40, new PumbilitySpotBox(4, 9.5, 20)),
                new ChartPresenceRow(chart, 1, false, 0, null, Array.Empty<double>(), 6, new PumbilitySpotBox(18, 21, 30))
            });

        await Repository().Replace(MixEnum.Phoenix2, census, ComputedAt, CancellationToken.None);

        var columns = await Repository().GetColumns(MixEnum.Phoenix2, CancellationToken.None);
        Assert.Equal(ComputedAt, columns.ComputedAt);
        Assert.Equal(census.Columns, columns.Rows);
        var rows = await Repository().GetRows(MixEnum.Phoenix2, chart, CancellationToken.None);
        Assert.Equal(2, rows.Count);
        Assert.Equal((false, 3, 40), (rows[0].CountsBoardPlayers, rows[0].Holders, rows[0].FolderSpots));
        Assert.Equal(new PumbilitySpots(1.5, 2, 3, 7.25, 12), rows[0].Spots);
        Assert.Equal(new[] { 1.5, 3, 12 }, rows[0].Dots);
        Assert.Equal(new PumbilitySpotBox(4, 9.5, 20), rows[0].Folder);
        Assert.Null(rows[1].Spots);
        Assert.Empty(rows[1].Dots);
        Assert.Equal(new PumbilitySpotBox(18, 21, 30), rows[1].Folder);
    }

    [Fact]
    public async Task ACensusReplacesItsOwnMixWholeAndLeavesAnotherAlone()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var old = await seeder.SeedChartAsync(22);
        var current = await seeder.SeedChartAsync(22);
        var repository = Repository();
        await repository.Replace(MixEnum.Phoenix2, Census("[P.B] BRONZE", old), ComputedAt, CancellationToken.None);
        await repository.Replace(MixEnum.Phoenix, Census("[P.B] BRONZE", old), ComputedAt, CancellationToken.None);

        await repository.Replace(MixEnum.Phoenix2, Census("[P.B] SILVER", current), ComputedAt.AddDays(1),
            CancellationToken.None);

        var columns = await repository.GetColumns(MixEnum.Phoenix2, CancellationToken.None);
        Assert.Equal(Name.From("[P.B] SILVER"), Assert.Single(columns.Rows).Band);
        Assert.Equal(ComputedAt.AddDays(1), columns.ComputedAt);
        Assert.Empty(await repository.GetRows(MixEnum.Phoenix2, old, CancellationToken.None));
        Assert.Single(await repository.GetRows(MixEnum.Phoenix2, current, CancellationToken.None));
        Assert.Single(await repository.GetRows(MixEnum.Phoenix, old, CancellationToken.None));
    }

    [Fact]
    public async Task ACensusWhoseWriteFailsLeavesThePreviousCensusInPlace()
    {
        // The write's deletes run ahead of its inserts, so a failed insert is only harmless when both
        // commit together. A row naming a chart the catalog does not hold fails on its foreign key.
        var chart = await new TestDataSeeder(_fixture.DbContextFactory).SeedChartAsync(22);
        var repository = Repository();
        await repository.Replace(MixEnum.Phoenix2, Census("[P.B] BRONZE", chart), ComputedAt, CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.Replace(MixEnum.Phoenix2,
            Census("[P.B] SILVER", Guid.NewGuid()), ComputedAt.AddDays(1), CancellationToken.None));

        var columns = await repository.GetColumns(MixEnum.Phoenix2, CancellationToken.None);
        Assert.Equal(Name.From("[P.B] BRONZE"), Assert.Single(columns.Rows).Band);
        Assert.Equal(ComputedAt, columns.ComputedAt);
        Assert.Single(await repository.GetRows(MixEnum.Phoenix2, chart, CancellationToken.None));
    }

    private static ChartPresenceCensusResult Census(string band, Guid chart)
    {
        return new ChartPresenceCensusResult(new[] { new ChartPresenceColumnRow(true, 0, Name.From(band), 30, 30) },
            new[]
            {
                new ChartPresenceRow(chart, 0, true, 6, new PumbilitySpots(1, 2, 3, 4, 5), Array.Empty<double>(), 0,
                    null)
            });
    }
}
