using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Infrastructure;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Infrastructure;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Seasons slice 1a (docs/design/seasons.md D12, §6.4): every season-discriminated table carries
///     a query filter named AllTime, so a season row is invisible to every existing read until a
///     reader drops that filter by name. Each test plants a season row beside an all-time one and asks
///     the real reader, including the one raw-SQL path the filter cannot cover. The API approval suite
///     mocks the mediator and cannot see a row, which is why this proof lives here.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class SeasonFilterTests : IAsyncLifetime
{
    private const short Fall2026 = 20264;
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public SeasonFilterTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // A fresh cache per repository, so every read here goes to the database.
    private EFPhoenixRecordsRepository Records() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()), Mock.Of<IChartRepository>(),
            new EFXXChartAttemptRepository(_fixture.DbContextFactory), Mock.Of<IMediator>(),
            Mock.Of<IPlayerStatsReader>(), new PeerScoreStore(_fixture.DbContextFactory));

    private async Task Plant<TEntity>(TEntity row) where TEntity : class
    {
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        database.Add(row);
        await database.SaveChangesAsync();
    }

    private async Task<int> CountPastTheFilter<TEntity>(short season) where TEntity : class
    {
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await database.Set<TEntity>().IgnoreQueryFilters()
            .CountAsync(e => EF.Property<short>(e, "SeasonId") == season);
    }

    [Fact]
    public async Task ARecordReadSeesTheAllTimeBestAndNotTheSeasonsRow()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Records().UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, Now));
        // A better score, but the season's — a reader that leaked it would print a best the player
        // never set all-time.
        await Plant(new PhoenixRecordEntity
        {
            Id = Guid.NewGuid(), UserId = userId, ChartId = chartId, MixId = TestDataSeeder.PhoenixMixId,
            SeasonId = Fall2026, Score = 999_000, LetterGrade = "SSS", Plate = "PG", IsBroken = false,
            RecordedDate = Now
        });

        var own = await Records().GetRecordedScore(MixEnum.Phoenix, userId, chartId);
        var board = (await Records().GetRecordedUserScores(MixEnum.Phoenix, chartId)).ToArray();
        var peers = await new PeerScoreStore(_fixture.DbContextFactory)
            .OnCharts(MixEnum.Phoenix, new[] { userId }, new[] { chartId }, CancellationToken.None);

        Assert.Equal(950_000, (int)own!.Score!.Value);
        Assert.Equal(950_000, (int)Assert.Single(board).Score);
        Assert.Equal(950_000, (int)Assert.Single(peers).Score);
        Assert.Equal(1, await CountPastTheFilter<PhoenixRecordEntity>(Fall2026));
    }

    [Fact]
    public async Task AStatsReadSeesTheAllTimeRowAndNotTheSeasonsRow()
    {
        var userId = Guid.NewGuid();
        var repository = new EFPlayerStatsRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
        await repository.SaveStats(MixEnum.Phoenix2, userId,
            new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 100, 0, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0),
            CancellationToken.None);
        await Plant(new PlayerStatsEntity
        {
            UserId = userId, MixId = MixIds.For(MixEnum.Phoenix2), SeasonId = Fall2026, SkillRating = 99_999,
            SinglesRating = 99_999
        });

        var fresh = new EFPlayerStatsRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
        var one = await fresh.GetStats(MixEnum.Phoenix2, userId, CancellationToken.None);
        var many = (await fresh.GetStats(MixEnum.Phoenix2, new[] { userId }, CancellationToken.None)).ToArray();

        Assert.Equal(100, one.SkillRating);
        Assert.Equal(100, Assert.Single(many).SkillRating);
        Assert.Equal(1, await CountPastTheFilter<PlayerStatsEntity>(Fall2026));
    }

    [Fact]
    public async Task AFolderReadSeesTheAllTimeFolderAndNotTheSeasonsRow()
    {
        var userId = Guid.NewGuid();
        var repository = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);
        await repository.Save(userId,
            new[] { new FolderLevelRecord(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22), 97, 60, 930_000, 930_000) },
            Now, CancellationToken.None);
        await Plant(new PlayerFolderLevelEntity
        {
            UserId = userId, MixId = MixIds.For(MixEnum.Phoenix), SeasonId = Fall2026, ChartType = "Single",
            Level = 22, Size = 97, Played = 3, AverageScore = 900_000, TierScore = 0, UpdatedAt = Now
        });

        var levels = (await repository.GetFolderLevels(MixEnum.Phoenix, userId, CancellationToken.None)).ToArray();

        Assert.Equal(60, Assert.Single(levels).Played);
        Assert.Equal(1, await CountPastTheFilter<PlayerFolderLevelEntity>(Fall2026));
    }

    [Fact]
    public async Task TheHardmodeCensusRewritesOnlyTheAllTimeListAndReadsOnlyIt()
    {
        var kept = await _seed.SeedPhoenixChartAsync(20);
        var seasonal = await _seed.SeedPhoenixChartAsync(21);
        var repository = new EFHardmodeChartRepository(_fixture.DbContextFactory);
        var census = new[] { new HardmodeChartRecord(kept, ChartType.Single, 20, 10, 3, 50, 5) };
        var staple = await _seed.SeedPhoenixChartAsync(20);
        var mostHeld = new[] { new HardmodeChartRecord(staple, ChartType.Single, 20, 900, 40, 50, 12) };
        await repository.Replace(MixEnum.Phoenix, census, mostHeld, Now, CancellationToken.None);
        // The copy a season takes at its roll (D31): the weekly census must neither read nor
        // delete it, or the season's locked list would drift with the all-time one. The most-held
        // end (hardmode-leaderboard.md D32) carries the same season key and must behave the same.
        await Plant(new HardmodeChartEntity
        {
            SeasonId = Fall2026, MixId = MixIds.For(MixEnum.Phoenix), ChartId = seasonal, Level = 21, Points = 1,
            Holders = 1, FolderSize = 50, FolderCut = 5, ComputedAt = Now
        });
        await Plant(new MostHeldChartEntity
        {
            SeasonId = Fall2026, MixId = MixIds.For(MixEnum.Phoenix), ChartId = seasonal, Level = 21, Points = 700,
            Holders = 30, FolderSize = 50, FolderCut = 12, ComputedAt = Now
        });

        await repository.Replace(MixEnum.Phoenix, census, mostHeld, Now.AddDays(7), CancellationToken.None);
        var list = await repository.Get(MixEnum.Phoenix, CancellationToken.None);
        var held = await repository.GetMostHeld(MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal(kept, Assert.Single(list).ChartId);
        Assert.Equal(staple, Assert.Single(held).ChartId);
        Assert.Equal(1, await CountPastTheFilter<HardmodeChartEntity>(Fall2026));
        Assert.Equal(1, await CountPastTheFilter<MostHeldChartEntity>(Fall2026));
    }

    [Fact]
    public async Task TheWarmedPeerStoreHoldsOnlyAllTimeBests()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Records().UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, Now));
        await Plant(new PhoenixRecordEntity
        {
            Id = Guid.NewGuid(), UserId = userId, ChartId = chartId, MixId = TestDataSeeder.PhoenixMixId,
            SeasonId = Fall2026, Score = 999_000, LetterGrade = "SSS", Plate = "PG", IsBroken = false,
            RecordedDate = Now
        });

        // Warm takes the everyone statement — the one tuned from five minutes to fifteen seconds and
        // the busier of the store's two raw reads. The score written behind the warm proves the read
        // below was served from it rather than fetched again through the named-few statement.
        var store = new PeerScoreStore(_fixture.DbContextFactory);
        await store.Warm(MixEnum.Phoenix, CancellationToken.None);
        await Records().UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(960_000), PhoenixPlate.SuperbGame, false, Now));

        var peers = await store.OnCharts(MixEnum.Phoenix, new[] { userId }, new[] { chartId }, CancellationToken.None);

        Assert.Equal(950_000, (int)Assert.Single(peers).Score);
    }

    // Slice 1b: the reads that name a season drop the AllTime filter by name and see that season's
    // rows instead (docs/design/seasons.md D12, §12.3) — a second row per chart, never the first one.
    [Fact]
    public async Task AReadThatNamesTheSeasonSeesTheSeasonsRowAndNotTheAllTimeOne()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        var records = Records();
        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, Now));
        // The season's pool starts empty, so a lower score is still its best.
        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(900_000), PhoenixPlate.RoughGame, false, Now), Fall);

        var own = await records.GetRecordedScore(MixEnum.Phoenix, userId, chartId, Fall);
        var board = (await records.GetRecordedUserScores(MixEnum.Phoenix, chartId, Fall)).ToArray();
        var bests = (await ((IScoreReader)records).GetBestScores(MixEnum.Phoenix, userId, Fall, CancellationToken.None))
            .ToArray();
        var allTime = await records.GetRecordedScore(MixEnum.Phoenix, userId, chartId);

        Assert.Equal(900_000, (int)own!.Score!.Value);
        Assert.Equal(900_000, (int)Assert.Single(board).Score);
        Assert.Equal(900_000, (int)Assert.Single(bests).Score!.Value);
        Assert.Equal(950_000, (int)allTime!.Score!.Value);
        Assert.Equal(1, await CountPastTheFilter<PhoenixRecordEntity>(Fall2026));
        Assert.Equal(1, await CountPastTheFilter<PhoenixRecordEntity>(0));
    }

    [Fact]
    public async Task DeletingASeasonsRecordLeavesTheAllTimeOneAndAWipeTakesBoth()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        var records = Records();
        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(950_000), PhoenixPlate.SuperbGame, false, Now));
        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(900_000), PhoenixPlate.RoughGame, false, Now), Fall);

        await records.DeleteRecord(MixEnum.Phoenix, userId, chartId, Fall);

        Assert.Equal(0, await CountPastTheFilter<PhoenixRecordEntity>(Fall2026));
        Assert.Equal(1, await CountPastTheFilter<PhoenixRecordEntity>(0));

        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(900_000), PhoenixPlate.RoughGame, false, Now), Fall);
        // The mix wipe is one delete that crosses seasons: it drops every filter rather than
        // running once per season it cannot enumerate from the Ledger.
        await records.DeleteAllForUser(userId, MixEnum.Phoenix);

        Assert.Equal(0, await CountPastTheFilter<PhoenixRecordEntity>(Fall2026));
        Assert.Equal(0, await CountPastTheFilter<PhoenixRecordEntity>(0));
    }

    [Fact]
    public async Task AStatsWriteThatNamesTheSeasonLandsBesideTheAllTimeRowAndReadsBackAlone()
    {
        var userId = Guid.NewGuid();
        var repository = new EFPlayerStatsRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
        await repository.SaveStats(MixEnum.Phoenix2, userId,
            new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 100, 0, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0),
            CancellationToken.None);
        await repository.SaveStats(MixEnum.Phoenix2, userId,
            new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 40, 0, 0, 40, 0, 0, 0, 0, 0, 0, 0, 0, TotalPumbility: 1234.5),
            Fall, CancellationToken.None);

        var fresh = new EFPlayerStatsRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()));
        var season = await fresh.GetStats(MixEnum.Phoenix2, userId, Fall, CancellationToken.None);
        var seasonMany = (await fresh.GetStats(MixEnum.Phoenix2, new[] { userId }, Fall, CancellationToken.None)).ToArray();
        var allTime = await fresh.GetStats(MixEnum.Phoenix2, userId, CancellationToken.None);
        var seasonIds = (await fresh.GetUserIdsWithStats(MixEnum.Phoenix2, Fall, CancellationToken.None)).ToArray();

        Assert.Equal(40, season.SkillRating);
        Assert.Equal(1234.5, season.TotalPumbility);
        Assert.Equal(40, Assert.Single(seasonMany).SkillRating);
        Assert.Equal(100, allTime.SkillRating);
        Assert.Equal(0, allTime.TotalPumbility);
        Assert.Equal(userId, Assert.Single(seasonIds));

        // The mix wipe takes every season's row in one delete.
        await fresh.DeleteStats(MixEnum.Phoenix2, userId, CancellationToken.None);

        Assert.Equal(0, await CountPastTheFilter<PlayerStatsEntity>(Fall2026));
        Assert.Equal(0, await CountPastTheFilter<PlayerStatsEntity>(0));
    }

    [Fact]
    public async Task AFolderWriteThatNamesTheSeasonLandsBesideTheAllTimeRowAndReadsBackAlone()
    {
        var userId = Guid.NewGuid();
        var repository = new EFPlayerFolderLevelRepository(_fixture.DbContextFactory);
        await repository.Save(userId,
            new[] { new FolderLevelRecord(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22), 97, 60, 930_000, 930_000) },
            Now, CancellationToken.None);
        await repository.Save(userId,
            new[] { new FolderLevelRecord(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22), 97, 3, 900_000, 0) },
            Now, Fall, CancellationToken.None);

        var season = (await repository.GetFolderLevels(MixEnum.Phoenix, userId, Fall, CancellationToken.None)).ToArray();
        var allTime = (await repository.GetFolderLevels(MixEnum.Phoenix, userId, CancellationToken.None)).ToArray();

        Assert.Equal(3, Assert.Single(season).Played);
        Assert.Equal(60, Assert.Single(allTime).Played);
        Assert.Equal(1, await CountPastTheFilter<PlayerFolderLevelEntity>(Fall2026));
        Assert.Equal(1, await CountPastTheFilter<PlayerFolderLevelEntity>(0));
    }

    [Fact]
    public async Task TheChartDictionaryOverlaysTheSeasonRatingWithoutMovingTheFolder()
    {
        var chartId = await _seed.SeedPhoenixChartAsync(22);
        // The roll rated this 22 a 21 for the season (D33): priced at 21, still filed under 22.
        await Plant(new ChartSeasonEntity
        {
            SeasonId = Fall2026, MixId = TestDataSeeder.PhoenixMixId, ChartId = chartId, Level = 21, PrintedLevel = 22,
            MovedThisRoll = -1
        });
        var repository = new EFChartRepository(new MemoryCache(new MemoryCacheOptions()), _fixture.DbContextFactory);

        var printed = (await repository.GetCharts(MixEnum.Phoenix, level: DifficultyLevel.From(22))).Single(c => c.Id == chartId);
        var inItsFolder = (await repository.GetCharts(MixEnum.Phoenix, Fall, DifficultyLevel.From(22)))
            .Single(c => c.Id == chartId);
        var inTheLowerFolder = (await repository.GetCharts(MixEnum.Phoenix, Fall, DifficultyLevel.From(21)))
            .Where(c => c.Id == chartId);
        var unfiltered = (await repository.GetCharts(MixEnum.Phoenix, Fall)).Single(c => c.Id == chartId);

        Assert.Equal(22, (int)printed.Level);
        Assert.Equal(21, (int)inItsFolder.Level);
        Assert.Empty(inTheLowerFolder);
        Assert.Equal(21, (int)unfiltered.Level);
    }
}
