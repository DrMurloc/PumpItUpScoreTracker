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

    /// <summary>A stats row with the two per-type pools; the merged total is their sum.</summary>
    private static PlayerStatsRecord Pools(Guid userId, double singles, double doubles) =>
        new(userId, 0, 1, 0, 0, 0, singles + doubles, 0, 0, singles, 0, 0, doubles, 0, 0, 0, 0, 0);

    [Fact]
    public async Task GetPlayersByPoolOfTypeIsInclusiveOnTheTypesOwnPoolAndScopedToTheMix()
    {
        // The window of a 17,500 singles pool (D53): 17,000 to 17,750, both ends in — a distance
        // from a pool, not a rung with a next start. The doubles pool never enters a singles read,
        // the merged total enters neither, and another mix's rows are not this mix's.
        var onTheFloor = Guid.NewGuid();   // singles 17,000.00 — in
        var inside = Guid.NewGuid();       // singles 17,500.00 — in
        var onTheCeiling = Guid.NewGuid(); // singles 17,750.00 — in
        var justAbove = Guid.NewGuid();    // singles 17,750.01 — out
        var below = Guid.NewGuid();        // singles 16,999.99 — out
        var doublesOnly = Guid.NewGuid();  // singles 9,000, doubles 17,500 — out of a singles read, in a doubles one
        var otherMix = Guid.NewGuid();     // singles 17,500 on Phoenix — out, wrong mix
        var repo = BuildRepository();
        await repo.SaveStats(MixEnum.Phoenix2, onTheFloor, Pools(onTheFloor, 17_000.00, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix2, inside, Pools(inside, 17_500.00, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix2, onTheCeiling, Pools(onTheCeiling, 17_750.00, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix2, justAbove, Pools(justAbove, 17_750.01, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix2, below, Pools(below, 16_999.99, 0), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix2, doublesOnly, Pools(doublesOnly, 9_000, 17_500), CancellationToken.None);
        await repo.SaveStats(MixEnum.Phoenix, otherMix, Pools(otherMix, 17_500, 0), CancellationToken.None);

        var singles = (await repo.GetPlayersByPoolOfType(MixEnum.Phoenix2, ChartType.Single, 17_000, 17_750,
            CancellationToken.None)).ToHashSet();
        var doubles = (await repo.GetPlayersByPoolOfType(MixEnum.Phoenix2, ChartType.Double, 17_000, 17_750,
            CancellationToken.None)).ToHashSet();

        Assert.Equal(new HashSet<Guid> { onTheFloor, inside, onTheCeiling }, singles);
        Assert.Equal(new HashSet<Guid> { doublesOnly }, doubles);
    }
}
