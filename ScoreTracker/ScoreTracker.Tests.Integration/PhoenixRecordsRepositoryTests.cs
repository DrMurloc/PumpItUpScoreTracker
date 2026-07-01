using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class PhoenixRecordsRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public PhoenixRecordsRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Each call returns a repo with a fresh MemoryCache, so a read built via BuildRepository()
    // goes to the database rather than seeing the writer's in-process cache.
    private EFPhoenixRecordsRepository BuildRepository() =>
        new(_fixture.DbContextFactory,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IChartRepository>(),
            new EFXXChartAttemptRepository(_fixture.DbContextFactory));

    [Fact]
    public async Task UpdateBestAttemptInsertsANewRecordReadableViaGetRecordedScore()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedChartAsync();

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(950000), PhoenixPlate.SuperbGame, IsBroken: false, RecordedAt));

        var retrieved = await BuildRepository().GetRecordedScore(userId, chartId);

        Assert.NotNull(retrieved);
        Assert.Equal(chartId, retrieved!.ChartId);
        Assert.Equal(950000, (int)retrieved.Score!.Value);
        Assert.Equal(PhoenixPlate.SuperbGame, retrieved.Plate);
        Assert.False(retrieved.IsBroken);
        Assert.Equal(RecordedAt, retrieved.RecordedDate);
    }

    [Fact]
    public async Task UpdateBestAttemptOverwritesAnExistingRecordWithoutGuardingAgainstLowerScores()
    {
        // The repo intentionally has no best-attempt protection — it just upserts. The guard against
        // overwriting a higher score with a lower one lives upstream in OfficialLeaderboardSaga's
        // import filter (the `toSave` predicate). Pinning that here so a refactor doesn't accidentally
        // move the guard into the repo without us noticing.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedChartAsync();

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(980000), PhoenixPlate.ExtremeGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(950000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var retrieved = await BuildRepository().GetRecordedScore(userId, chartId);

        Assert.NotNull(retrieved);
        Assert.Equal(950000, (int)retrieved!.Score!.Value);
        Assert.Equal(PhoenixPlate.SuperbGame, retrieved.Plate);
    }

    [Fact]
    public async Task GetRecordedScoresReturnsOnlyTheRequestedUsersRecords()
    {
        var userA = await _seed.SeedUserAsync();
        var userB = await _seed.SeedUserAsync();
        var chartX = await _seed.SeedChartAsync();
        var chartY = await _seed.SeedChartAsync();

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userA, new RecordedPhoenixScore(chartX,
            PhoenixScore.From(900000), PhoenixPlate.TalentedGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userA, new RecordedPhoenixScore(chartY,
            PhoenixScore.From(910000), PhoenixPlate.MarvelousGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userB, new RecordedPhoenixScore(chartX,
            PhoenixScore.From(920000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var reader = BuildRepository();
        var userAScores = (await reader.GetRecordedScores(userA)).ToList();
        var userBScores = (await reader.GetRecordedScores(userB)).ToList();

        Assert.Equal(2, userAScores.Count);
        Assert.Contains(userAScores, s => s.ChartId == chartX);
        Assert.Contains(userAScores, s => s.ChartId == chartY);
        Assert.Single(userBScores);
        Assert.Equal(chartX, userBScores[0].ChartId);
    }

    [Fact]
    public async Task GetRecordedScoreReturnsNullWhenNoRecordExists()
    {
        var retrieved = await BuildRepository().GetRecordedScore(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Theory]
    [InlineData(PhoenixPlate.RoughGame)]
    [InlineData(PhoenixPlate.FairGame)]
    [InlineData(PhoenixPlate.TalentedGame)]
    [InlineData(PhoenixPlate.MarvelousGame)]
    [InlineData(PhoenixPlate.SuperbGame)]
    [InlineData(PhoenixPlate.ExtremeGame)]
    [InlineData(PhoenixPlate.UltimateGame)]
    [InlineData(PhoenixPlate.PerfectGame)]
    public async Task PlateRoundTripsThroughTheStringColumnForEveryEnumValue(PhoenixPlate plate)
    {
        // The entity persists `Plate` as a string via `GetName()` and parses it back via
        // `PhoenixPlateHelperMethods.TryParse`. If either side drifts, an enum value silently
        // becomes null on read. This theory catches that for every variant.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedChartAsync();

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(900000), plate, false, RecordedAt));

        var retrieved = await BuildRepository().GetRecordedScore(userId, chartId);

        Assert.NotNull(retrieved);
        Assert.Equal(plate, retrieved!.Plate);
    }

    [Fact]
    public async Task LevelRangeQueryReturnsOnlyChartsWhosePhoenixMixLevelIsInRange()
    {
        // Exercises the ChartMix → Chart → PhoenixBestAttempt join chain plus the level-range
        // WHERE clause. Catches breakage in the SQL when migrations move columns or the join
        // semantics shift under a refactor.
        var userId = await _seed.SeedUserAsync();
        var chart13 = await _seed.SeedPhoenixChartAsync(level: 13);
        var chart15 = await _seed.SeedPhoenixChartAsync(level: 15);
        var chart17 = await _seed.SeedPhoenixChartAsync(level: 17);

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userId,
            new RecordedPhoenixScore(chart13, PhoenixScore.From(900000), PhoenixPlate.MarvelousGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userId,
            new RecordedPhoenixScore(chart15, PhoenixScore.From(920000), PhoenixPlate.MarvelousGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userId,
            new RecordedPhoenixScore(chart17, PhoenixScore.From(940000), PhoenixPlate.MarvelousGame, false, RecordedAt));

        var inRange = (await BuildRepository().GetRecordedScores(
            new[] { userId }, ChartType.Single, 14, 16, CancellationToken.None)).ToList();

        Assert.Single(inRange);
        Assert.Equal(chart15, inRange[0].ChartId);
    }

    [Fact]
    public async Task LevelRangeQueryFiltersByChartType()
    {
        var userId = await _seed.SeedUserAsync();
        var singleChart = await _seed.SeedPhoenixChartAsync(level: 15, type: "Single");
        var doubleChart = await _seed.SeedPhoenixChartAsync(level: 15, type: "Double");

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(userId,
            new RecordedPhoenixScore(singleChart, PhoenixScore.From(900000), PhoenixPlate.SuperbGame, false, RecordedAt));
        await writer.UpdateBestAttempt(userId,
            new RecordedPhoenixScore(doubleChart, PhoenixScore.From(900000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var singles = (await BuildRepository().GetRecordedScores(
            new[] { userId }, ChartType.Single, 15, 15, CancellationToken.None)).ToList();

        Assert.Single(singles);
        Assert.Equal(singleChart, singles[0].ChartId);
    }

    [Fact]
    public async Task LevelRangeQueryReturnsOnlyScoresForTheRequestedUserIds()
    {
        // Regression: prior to the 2026-05 fix, this overload accepted `userIds` but never used it
        // in the WHERE clause, returning every user's scores in the level range. That broke skill-
        // cohort weighting in `TierListSaga.ProcessPassTierList` and the cohort-percentile basis
        // of `PumbilityProjectionSaga`. This test pins the corrected filter so it can't silently
        // regress. RecordedPhoenixScore doesn't expose UserId, so we differentiate by score value.
        var requested = await _seed.SeedUserAsync();
        var unrequested = await _seed.SeedUserAsync();
        var chart = await _seed.SeedPhoenixChartAsync(level: 15, type: "Single");

        var writer = BuildRepository();
        await writer.UpdateBestAttempt(requested,
            new RecordedPhoenixScore(chart, PhoenixScore.From(900000), PhoenixPlate.MarvelousGame, false, RecordedAt));
        await writer.UpdateBestAttempt(unrequested,
            new RecordedPhoenixScore(chart, PhoenixScore.From(950000), PhoenixPlate.SuperbGame, false, RecordedAt));

        var scores = (await BuildRepository().GetRecordedScores(
            new[] { requested }, ChartType.Single, 15, 15, CancellationToken.None)).ToList();

        Assert.Single(scores);
        Assert.Equal(900000, (int)scores[0].Score!.Value);
        Assert.Equal(PhoenixPlate.MarvelousGame, scores[0].Plate);
    }
}
