using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The Ledger's peer-score store against a real database
///     (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         What the repository's own tests cannot reach: that a slice really is held between reads,
///         that eviction is what releases it, and that holding it does not change any of the answers
///         the SQL used to give — the masked name, the walkoff, the chart set that spans folders.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class PeerScoreStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public PeerScoreStoreTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private PeerScoreStore Store() => new(_fixture.DbContextFactory);

    // A writer that goes straight to the table, so a test can move a score behind a store's back —
    // which is exactly what an import on another instance would look like.
    private EFPhoenixRecordsRepository Writer() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IChartRepository>(), new EFXXChartAttemptRepository(_fixture.DbContextFactory),
            Mock.Of<IMediator>(), Mock.Of<IPlayerStatsReader>(), Store());

    [Fact]
    public async Task AScoreWrittenBehindTheStoreIsNotSeenUntilThePlayerIsEvicted()
    {
        // The whole point of the store, and the whole risk of it. The event is the mechanism
        // (PeerScoreCacheConsumer); this proves both halves — that a read really is held, and
        // that Evict is what lets the next one through.
        var userId = await _seed.SeedUserAsync();
        var first = await _seed.SeedPhoenixChartAsync(20);
        var second = await _seed.SeedPhoenixChartAsync(20);
        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(first,
            PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var store = Store();
        var before = await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None);

        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(second,
            PhoenixScore.From(970_000), PhoenixPlate.SuperbGame, false, RecordedAt));
        var stale = await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None);

        store.Evict(userId, MixEnum.Phoenix);
        var after = await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None);

        Assert.Equal(first, Assert.Single(before).ChartId);
        Assert.Equal(first, Assert.Single(stale).ChartId);
        Assert.Equal(new[] { first, second }.OrderBy(g => g), after.Select(r => r.ChartId).OrderBy(g => g));
    }

    [Fact]
    public async Task EvictingOneMixLeavesTheOther()
    {
        // A wipe of one mix is not a wipe of the player. PlayerScoreDataDeletedEvent carries a
        // mix precisely so that a Phoenix 2 deletion does not blank the Phoenix 1 peer group.
        var userId = await _seed.SeedUserAsync();
        var phoenix = await _seed.SeedPhoenixChartAsync(20);
        var extra = await _seed.SeedPhoenixChartAsync(20);
        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(phoenix,
            PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var store = Store();
        await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None);

        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(extra,
            PhoenixScore.From(960_000), PhoenixPlate.SuperbGame, false, RecordedAt));
        store.Evict(userId, MixEnum.Phoenix2);

        var held = await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None);

        Assert.Equal(phoenix, Assert.Single(held).ChartId);
    }

    [Fact]
    public async Task APrivatePlayerIsMaskedAndSaysSo()
    {
        // The contract the SQL always had: the name arrives already masked and IsPublic says
        // outright that it was, so no consumer has to recognise the mask by its text.
        var userId = await _seed.SeedUserAsync("Hidden Player", false);
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var row = Assert.Single(await Store().InLevelRange(MixEnum.Phoenix, new[] { userId },
            ChartType.Single, 20, 20, CancellationToken.None));

        Assert.False(row.IsPublic);
        Assert.Equal("Anonymous", row.UserName.ToString());
    }

    [Fact]
    public async Task ABrokenRunNeverEntersEitherRead()
    {
        // Cohort-only, both ways in: a walkoff would make everyone else's percentile look better
        // than it is, and the chart-set read feeds the same machinery the band read does.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(410_000), null, true, RecordedAt));

        var store = Store();

        Assert.Empty(await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None));
        Assert.Empty(await store.OnCharts(MixEnum.Phoenix, new[] { userId }, new[] { chartId },
            CancellationToken.None));
    }

    [Fact]
    public async Task TheChartSetReadCrossesTypesAndLevelsTheBandReadWouldNotHold()
    {
        // A chart dialog asks about whatever chart the viewer opened, so the store holds a player
        // whole — every type and every level, CO-OP and a level the pool would never price
        // included — and lets the band read narrow instead.
        var userId = await _seed.SeedUserAsync();
        var singles = await _seed.SeedPhoenixChartAsync(21);
        var doubles = await _seed.SeedPhoenixChartAsync(24, "Double");
        var coop = await _seed.SeedPhoenixChartAsync(3, "CoOp");
        var writer = Writer();
        foreach (var chartId in new[] { singles, doubles, coop })
            await writer.UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(chartId,
                PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var store = Store();
        var everything = await store.OnCharts(MixEnum.Phoenix, new[] { userId },
            new[] { singles, doubles, coop }, CancellationToken.None);
        var band = await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 21, 21,
            CancellationToken.None);

        Assert.Equal(new[] { singles, doubles, coop }.OrderBy(g => g),
            everything.Select(r => r.ChartId).OrderBy(g => g));
        Assert.Equal(singles, Assert.Single(band).ChartId);
    }

    [Fact]
    public async Task APlayerWithNothingIsAnAnswerRatherThanAnError()
    {
        // A peer group holds people who have passed nothing in the band, and asking twice must
        // not mean querying twice — the store stores the emptiness.
        var userId = await _seed.SeedUserAsync();
        await _seed.SeedPhoenixChartAsync(20);
        var store = Store();

        Assert.Empty(await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None));
        Assert.Empty(await store.InLevelRange(MixEnum.Phoenix, new[] { userId }, ChartType.Single, 20, 20,
            CancellationToken.None));
    }

    [Fact]
    public async Task WarmingTheMixAnswersForAPlayerNobodyHasAskedAbout()
    {
        // What the startup warm-up buys: a peer the first viewer has never named is already held.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Writer().UpdateBestAttempt(MixEnum.Phoenix, userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var store = Store();
        await store.Warm(MixEnum.Phoenix, CancellationToken.None);

        var read = Assert.Single(await store.InLevelRange(MixEnum.Phoenix, new[] { userId },
            ChartType.Single, 20, 20, CancellationToken.None));

        Assert.Equal(chartId, read.ChartId);
    }
}
