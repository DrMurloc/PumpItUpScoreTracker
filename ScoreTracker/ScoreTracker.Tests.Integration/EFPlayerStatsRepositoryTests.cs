using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Real-SQL coverage for the player-stats peer reads (the PUMBILITY pool window): the range
///     filters and the per-type column selection run in SQL Server, not in memory. Seeds through
///     the repo's own SaveStats (same posture as EFUserRepositoryTests). The competitive-neighbours
///     facts retired with the Account Stats widget's old match list (2026-09-05).
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
