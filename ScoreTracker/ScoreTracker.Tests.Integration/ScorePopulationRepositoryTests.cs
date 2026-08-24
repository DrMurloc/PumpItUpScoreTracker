using Microsoft.EntityFrameworkCore;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ScorePopulationRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public ScorePopulationRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFScorePopulationRepository BuildRepository() => new(_fixture.DbContextFactory);

    private async Task SeedRecord(Guid chartId, int score, bool isBroken = false,
        int? perfects = null, int greats = 0, int goods = 0, int bads = 0, int misses = 0,
        int? maxCombo = null)
    {
        // Every record needs a real owner -- the table holds an FK onto User.
        var userId = await _seed.SeedUserAsync();
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        ctx.Set<PhoenixRecordEntity>().Add(new PhoenixRecordEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChartId = chartId,
            MixId = TestDataSeeder.PhoenixMixId,
            RecordedDate = Now,
            Score = score,
            IsBroken = isBroken,
            Perfects = perfects,
            Greats = perfects == null ? null : greats,
            Goods = perfects == null ? null : goods,
            Bads = perfects == null ? null : bads,
            Misses = perfects == null ? null : misses,
            MaxCombo = maxCombo
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task PopulationBandsNonBrokenSinglesAndDoublesBestsByTheMixesLevel()
    {
        var single18 = await _seed.SeedPhoenixChartAsync(18);
        var double20 = await _seed.SeedPhoenixChartAsync(20, "Double");
        var coOp = await _seed.SeedPhoenixChartAsync(3, "CoOp");
        // One best per band on the S18, one below the line and one broken that must not count.
        foreach (var score in new[] { 890_000, 910_000, 955_000, 973_000, 984_000, 991_000, 997_000 })
            await SeedRecord(single18, score);
        await SeedRecord(single18, 999_000, isBroken: true);
        await SeedRecord(double20, 940_000);
        await SeedRecord(coOp, 990_000);

        var population = await BuildRepository().GetPopulationByLevel(MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal(2, population.Count);
        var s18 = population.Single(p => p.Level == 18);
        Assert.Equal(7, s18.Total);
        Assert.Equal(1, s18.Below900k);
        Assert.Equal(1, s18.From900k);
        Assert.Equal(1, s18.From950k);
        Assert.Equal(1, s18.From970k);
        Assert.Equal(1, s18.From980k);
        Assert.Equal(1, s18.From990k);
        Assert.Equal(1, s18.From995k);
        var d20 = population.Single(p => p.Level == 20);
        Assert.Equal(1, d20.Total);
        Assert.Equal(1, d20.From900k);
    }

    [Fact]
    public async Task JudgedBestsCarryOnlyJudgementBearingNonBrokenRows()
    {
        var chart = await _seed.SeedPhoenixChartAsync();
        await SeedRecord(chart, 985_000, perfects: 950, greats: 30, goods: 10, bads: 5, misses: 5,
            maxCombo: 400);
        await SeedRecord(chart, 700_000, isBroken: true, perfects: 500, greats: 100, misses: 400);
        await SeedRecord(chart, 960_000);

        var judged = await BuildRepository().GetJudgedBests(MixEnum.Phoenix, CancellationToken.None);

        var best = Assert.Single(judged);
        Assert.Equal(985_000, best.Score);
        Assert.Equal(950, best.Perfects);
        Assert.Equal(5, best.Misses);
        Assert.Equal(400, best.MaxCombo);
    }
}
