using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFTierListRepositoryTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public EFTierListRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // GetAllEntries caches per TierListName for 24 hours; a fresh repo on read forces the DB path.
    // The activity reader is the real Ledger implementation (the subject of GetUsersOnLevel's
    // requireActive path); IChartRepository inside it is an incidental collaborator.
    private EFTierListRepository BuildRepository() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()),
            new EFPhoenixRecordsRepository(_fixture.DbContextFactory,
                new MemoryCache(new MemoryCacheOptions()), new Mock<IChartRepository>().Object));

    [Fact]
    public async Task SaveEntryAndGetAllEntriesRoundTripPreservesAllFields()
    {
        var chartId = Guid.NewGuid();
        var entry = new SongTierListEntry("PassCount", chartId, TierListCategory.Easy, Order: 5);

        await BuildRepository().SaveEntry(entry, CancellationToken.None);

        var entries = (await BuildRepository().GetAllEntries("PassCount", CancellationToken.None)).ToList();

        Assert.Single(entries);
        Assert.Equal(chartId, entries[0].ChartId);
        Assert.Equal(TierListCategory.Easy, entries[0].Category);
        Assert.Equal(5, entries[0].Order);
        Assert.Equal("PassCount", (string)entries[0].TierListName);
    }

    [Fact]
    public async Task SaveEntryUpdatesExistingRowForSameTierListNameAndChartId()
    {
        var chartId = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveEntry(new SongTierListEntry("PassCount", chartId, TierListCategory.Easy, 1),
            CancellationToken.None);
        await writer.SaveEntry(new SongTierListEntry("PassCount", chartId, TierListCategory.Hard, 9),
            CancellationToken.None);

        var entries = (await BuildRepository().GetAllEntries("PassCount", CancellationToken.None)).ToList();

        Assert.Single(entries);
        Assert.Equal(TierListCategory.Hard, entries[0].Category);
        Assert.Equal(9, entries[0].Order);
    }

    [Fact]
    public async Task GetAllEntriesReturnsOnlyTheRequestedTierListName()
    {
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveEntry(new SongTierListEntry("PassCount", chartA, TierListCategory.Easy, 1),
            CancellationToken.None);
        await writer.SaveEntry(new SongTierListEntry("Popularity", chartB, TierListCategory.Hard, 2),
            CancellationToken.None);

        var reader = BuildRepository();
        var passCount = (await reader.GetAllEntries("PassCount", CancellationToken.None)).ToList();
        var popularity = (await reader.GetAllEntries("Popularity", CancellationToken.None)).ToList();

        Assert.Single(passCount);
        Assert.Equal(chartA, passCount[0].ChartId);
        Assert.Single(popularity);
        Assert.Equal(chartB, popularity[0].ChartId);
    }

    [Fact]
    public async Task SaveEntriesBulkHandlesMixedInsertsAndUpdatesInOnePass()
    {
        // SaveEntries is the recurring-job hot path (TierListSaga calls it after computing weights).
        // It batches all (TierListName, ChartId) keys and decides insert vs update per-entry.
        var existingChart = Guid.NewGuid();
        var newChart = Guid.NewGuid();
        var writer = BuildRepository();
        await writer.SaveEntry(new SongTierListEntry("PassCount", existingChart, TierListCategory.Easy, 1),
            CancellationToken.None);

        await writer.SaveEntries(new[]
        {
            new SongTierListEntry("PassCount", existingChart, TierListCategory.Hard, 5), // update
            new SongTierListEntry("PassCount", newChart, TierListCategory.Medium, 3) // insert
        }, CancellationToken.None);

        var entries = (await BuildRepository().GetAllEntries("PassCount", CancellationToken.None))
            .OrderBy(e => e.Order).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal(newChart, entries[0].ChartId);
        Assert.Equal(TierListCategory.Medium, entries[0].Category);
        Assert.Equal(existingChart, entries[1].ChartId);
        Assert.Equal(TierListCategory.Hard, entries[1].Category);
    }

    [Fact]
    public async Task GetUsersOnLevelReturnsOnlyUsersAtTheRequestedHighestTitleLevel()
    {
        // GetUsersOnLevel feeds TierListSaga's weight buckets — the saga calls it with multiple
        // levels and combines results. After today's userId-WHERE-clause fix, the saga's downstream
        // call to GetRecordedScores actually filters by these returned userIds. Pin the source.
        var userL15 = Guid.NewGuid();
        var userL16 = Guid.NewGuid();
        var userL17 = Guid.NewGuid();

        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            ctx.UserHighestTitle.AddRange(
                new UserHighestTitleEntity { UserId = userL15, TitleName = "title15", Level = 15 },
                new UserHighestTitleEntity { UserId = userL16, TitleName = "title16", Level = 16 },
                new UserHighestTitleEntity { UserId = userL17, TitleName = "title17", Level = 17 });
            await ctx.SaveChangesAsync();
        }

        var atLevel16 = (await BuildRepository().GetUsersOnLevel(16, CancellationToken.None)).ToList();

        Assert.Single(atLevel16);
        Assert.Contains(userL16, atLevel16);
    }

    [Fact]
    public async Task GetUsersOnLevelWithRequireActiveKeepsOnlyRecentlyActivePlayers()
    {
        // Activity comes from IScoreReader.GetActiveUserIds (the Ledger read contract) instead
        // of a SQL join onto the Ledger's PhoenixRecord table (rearch C36) — this pins that the
        // set-intersection rewrite filters the same way the join did.
        var seeder = new TestDataSeeder(_fixture.DbContextFactory);
        var activeUser = await seeder.SeedUserAsync();
        var staleUser = await seeder.SeedUserAsync();
        var chartId = await seeder.SeedChartAsync();

        await using (var ctx = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            ctx.UserHighestTitle.AddRange(
                new UserHighestTitleEntity { UserId = activeUser, TitleName = "t16a", Level = 16 },
                new UserHighestTitleEntity { UserId = staleUser, TitleName = "t16b", Level = 16 });
            // The cutoff is now-120d on the repository's own clock (pre-existing seam gap),
            // so the seed rows anchor to the real clock too.
            ctx.PhoenixBestAttempt.AddRange(
                new PhoenixRecordEntity
                {
                    Id = Guid.NewGuid(), UserId = activeUser, ChartId = chartId,
                    RecordedDate = DateTimeOffset.Now.AddDays(-1), Score = 900000, IsBroken = false
                },
                new PhoenixRecordEntity
                {
                    Id = Guid.NewGuid(), UserId = staleUser, ChartId = chartId,
                    RecordedDate = DateTimeOffset.Now.AddDays(-365), Score = 900000, IsBroken = false
                });
            await ctx.SaveChangesAsync();
        }

        var result = (await BuildRepository().GetUsersOnLevel(16, CancellationToken.None, requireActive: true))
            .ToList();

        Assert.Single(result);
        Assert.Contains(activeUser, result);
    }
}
