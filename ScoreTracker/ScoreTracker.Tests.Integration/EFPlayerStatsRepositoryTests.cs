using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Real-SQL coverage for the competitive-neighbours cohort query (the Account Stats
///     widget's match list): the range filter, the ABS ordering, the count cap, and the
///     per-dimension column selection all run in SQL Server, not in memory. Seeds through
///     the repo's own SaveStats (same posture as EFUserRepositoryTests).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFPlayerStatsRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFPlayerStatsRepositoryTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFPlayerStatsRepository BuildRepository() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));

    private static PlayerStatsRecord Stats(Guid userId, double singles, double doubles, double combined) =>
        new(userId, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, combined, singles, doubles);

    [Fact]
    public async Task GetCompetitiveNeighborsReturnsInRangeCandidatesNearestFirst()
    {
        var near = Guid.NewGuid();  // 21.36 → 0.02
        var close = Guid.NewGuid(); // 21.30 → 0.04
        var edge = Guid.NewGuid();  // 21.50 → 0.16 (still within ±1.0)
        var below = Guid.NewGuid(); // 20.10 → 1.24 (out)
        var above = Guid.NewGuid(); // 22.50 → 1.16 (out)
        var repo = BuildRepository();
        await repo.SaveStats(MixEnum.Phoenix, near, Stats(near, 21.36, 0, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix, close, Stats(close, 21.30, 0, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix, edge, Stats(edge, 21.50, 0, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix, below, Stats(below, 20.10, 0, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix, above, Stats(above, 22.50, 0, 0), CancellationToken.None);

        var result = (await repo.Handle(
            new GetCompetitiveNeighborsQuery(MixEnum.Phoenix, ChartType.Single, 21.34, 1.0, 10),
            CancellationToken.None)).ToArray();

        Assert.Equal(new[] { near, close, edge }, result.Select(n => n.UserId).ToArray());
        Assert.Equal(21.36, result[0].CompetitiveLevel, 2);
    }

    [Fact]
    public async Task GetCompetitiveNeighborsCapsAtTheRequestedCount()
    {
        var repo = BuildRepository();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            await repo.SaveStats(MixEnum.Phoenix, id, Stats(id, 21.30 + i * 0.01, 0, 0), CancellationToken.None);
        }

        var result = (await repo.Handle(
            new GetCompetitiveNeighborsQuery(MixEnum.Phoenix, ChartType.Single, 21.32, 1.0, 2),
            CancellationToken.None)).ToArray();

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task GetCompetitiveNeighborsRanksOnTheRequestedDimension()
    {
        // Strong singles, weak doubles: in range on Singles, out of range on Doubles.
        var id = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.SaveStats(MixEnum.Phoenix, id, Stats(id, 21.30, 18.00, 20.00), CancellationToken.None);

        var singles = await repo.Handle(
            new GetCompetitiveNeighborsQuery(MixEnum.Phoenix, ChartType.Single, 21.34, 1.0, 10),
            CancellationToken.None);
        var doubles = await repo.Handle(
            new GetCompetitiveNeighborsQuery(MixEnum.Phoenix, ChartType.Double, 21.34, 1.0, 10),
            CancellationToken.None);
        var combined = await repo.Handle(
            new GetCompetitiveNeighborsQuery(MixEnum.Phoenix, null, 20.20, 1.0, 10),
            CancellationToken.None);

        Assert.Contains(singles, n => n.UserId == id);       // 21.30 within ±1 of 21.34
        Assert.DoesNotContain(doubles, n => n.UserId == id); // 18.00 outside ±1 of 21.34
        Assert.Contains(combined, n => n.UserId == id);      // 20.00 within ±1 of 20.20
    }
}
