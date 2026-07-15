using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFChartSimilarityRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFChartSimilarityRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset ComputedAt = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private EFChartSimilarityRepository BuildRepository() => new(_fixture.DbContextFactory);

    [Fact]
    public async Task ReplaceEdgesAndGetEdgesRoundTripPreservesTheSignalBreakdown()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var chart = await seeder.SeedChartAsync();
        var neighbor = await seeder.SeedChartAsync();

        // Null sub-scores are meaningful (signal unavailable for the pair) and must survive
        // the SignalsJson round trip alongside the real ones.
        var edge = new ChartSimilarityEdge(neighbor, 0.87, SkillScore: 0.91, DifficultyScore: 0.8,
            PlayersScore: null, IntensityScore: 0.75, MetaScore: 0.2, SharedScorers: 12);
        await BuildRepository().ReplaceEdges(MixEnum.Phoenix, chart, new[] { edge }, ComputedAt,
            CancellationToken.None);

        var edges = await BuildRepository().GetEdges(MixEnum.Phoenix, chart, CancellationToken.None);

        var stored = Assert.Single(edges);
        Assert.Equal(neighbor, stored.SimilarChartId);
        Assert.Equal(0.87, stored.Score);
        Assert.Equal(0.91, stored.SkillScore);
        Assert.Equal(0.8, stored.DifficultyScore);
        Assert.Null(stored.PlayersScore);
        Assert.Equal(0.75, stored.IntensityScore);
        Assert.Equal(0.2, stored.MetaScore);
        Assert.Equal(12, stored.SharedScorers);
    }

    [Fact]
    public async Task ReplaceEdgesIsWholesalePerChart()
    {
        // The nightly job recomputes the full top-K — a neighbor absent from the new set is
        // stale by definition and must not survive the rebuild.
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var chart = await seeder.SeedChartAsync();
        var oldNeighbor = await seeder.SeedChartAsync();
        var newNeighbor = await seeder.SeedChartAsync();
        var writer = BuildRepository();
        await writer.ReplaceEdges(MixEnum.Phoenix, chart,
            new[] { new ChartSimilarityEdge(oldNeighbor, 0.9, null, null, null, null, null, 0) },
            ComputedAt, CancellationToken.None);

        await writer.ReplaceEdges(MixEnum.Phoenix, chart,
            new[] { new ChartSimilarityEdge(newNeighbor, 0.7, null, null, null, null, null, 0) },
            ComputedAt.AddDays(1), CancellationToken.None);

        var edges = await BuildRepository().GetEdges(MixEnum.Phoenix, chart, CancellationToken.None);
        var stored = Assert.Single(edges);
        Assert.Equal(newNeighbor, stored.SimilarChartId);
    }

    [Fact]
    public async Task GetEdgesReturnsBestMatchFirstAndOnlyTheRequestedMix()
    {
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var chart = await seeder.SeedChartAsync();
        var closest = await seeder.SeedChartAsync();
        var farther = await seeder.SeedChartAsync();
        var phoenix2Neighbor = await seeder.SeedChartAsync();
        var writer = BuildRepository();
        await writer.ReplaceEdges(MixEnum.Phoenix, chart, new[]
        {
            new ChartSimilarityEdge(farther, 0.6, null, null, null, null, null, 0),
            new ChartSimilarityEdge(closest, 0.92, null, null, null, null, null, 0)
        }, ComputedAt, CancellationToken.None);
        await writer.ReplaceEdges(MixEnum.Phoenix2, chart,
            new[] { new ChartSimilarityEdge(phoenix2Neighbor, 0.5, null, null, null, null, null, 0) },
            ComputedAt, CancellationToken.None);

        var phoenix = await BuildRepository().GetEdges(MixEnum.Phoenix, chart, CancellationToken.None);
        var phoenix2 = await BuildRepository().GetEdges(MixEnum.Phoenix2, chart, CancellationToken.None);

        Assert.Equal(new[] { closest, farther }, phoenix.Select(e => e.SimilarChartId).ToArray());
        Assert.Equal(new[] { phoenix2Neighbor }, phoenix2.Select(e => e.SimilarChartId).ToArray());
    }
}
