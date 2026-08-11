using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The Your Data cleanup, against a real database. The handler suite can prove the cleanup
///     asks for the right thing; only this can prove it removes the right rows — a mock has no
///     way to over-delete, which is the one failure that matters here (same reasoning as
///     <see cref="AccountPurgeTests" />).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class BrokenRecordCleanupTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public BrokenRecordCleanupTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // A fresh MemoryCache per call, so a read after the delete goes to the database rather than
    // seeing the writer's in-process copy of the scores it just removed.
    private EFPhoenixRecordsRepository BuildRepository() =>
        new(_fixture.DbContextFactory,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IChartRepository>(),
            new EFXXChartAttemptRepository(_fixture.DbContextFactory),
            Mock.Of<IMediator>(), Mock.Of<IPlayerStatsReader>());

    [Fact]
    public async Task TheCleanupTakesEveryBrokenRecordAndNothingElse()
    {
        var userId = await _seed.SeedUserAsync();
        var stranger = await _seed.SeedUserAsync();
        var brokenChart = await _seed.SeedPhoenixChartAsync(20);
        var secondBrokenChart = await _seed.SeedPhoenixChartAsync(21);
        var passedChart = await _seed.SeedPhoenixChartAsync(19);

        var writer = BuildRepository();
        await Record(writer, userId, brokenChart, 812_345, isBroken: true);
        await Record(writer, userId, secondBrokenChart, 654_321, isBroken: true);
        await Record(writer, userId, passedChart, 963_210, isBroken: false);
        // The decoy: a stranger's broken record on the same chart. Purging one player must not
        // move another's row, and no mocked repository can catch that.
        await Record(writer, stranger, brokenChart, 700_000, isBroken: true);

        var removed = await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        Assert.Equal(2, removed);
        var mine = (await BuildRepository().GetRecordedScores(MixEnum.Phoenix, userId)).ToArray();
        Assert.Equal(new[] { passedChart }, mine.Select(s => s.ChartId).ToArray());
        var theirs = (await BuildRepository().GetRecordedScores(MixEnum.Phoenix, stranger)).ToArray();
        Assert.Single(theirs);
        Assert.True(theirs[0].IsBroken);
    }

    [Fact]
    public async Task AnotherMixesBrokenRecordsAreLeftStanding()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await EnsurePhoenix2MixAsync();

        var writer = BuildRepository();
        await Record(writer, userId, chartId, 812_345, isBroken: true);
        await Record(writer, userId, chartId, 700_000, isBroken: true, mix: MixEnum.Phoenix2);

        var removed = await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix2, userId);

        Assert.Equal(1, removed);
        Assert.Single(await BuildRepository().GetRecordedScores(MixEnum.Phoenix, userId));
        Assert.Empty(await BuildRepository().GetRecordedScores(MixEnum.Phoenix2, userId));
    }

    [Fact]
    public async Task TheJournalKeepsEveryPlayTheCleanupWithdrew()
    {
        // The whole point of the design: the run happened, so it stays in the chart's history —
        // only its standing as the record is withdrawn.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        var journal = new EFScoreJournalRepository(_fixture.DbContextFactory);
        await journal.Append(new ScoreJournalEntry(RecordedAt, ScoreJournalEntry.OfficialImportSource, userId,
            chartId, PhoenixScore.From(812_345), null, IsBroken: true), CancellationToken.None);
        await Record(BuildRepository(), userId, chartId, 812_345, isBroken: true);

        await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        var history = (await journal.GetChartHistories(userId, new[] { chartId }, CancellationToken.None))
            .ToArray();
        Assert.Single(history);
        Assert.True(history[0].IsBroken);
        Assert.Equal(812_345, (int)history[0].Score!.Value);
    }

    [Fact]
    public async Task APerScoreStatsRowGoesWithItsRecordAndNoOtherDoes()
    {
        // The stats row is keyed by chart, not by brokenness — read after the records are gone
        // and there is nothing left to say which rows belonged to them.
        var userId = await _seed.SeedUserAsync();
        var brokenChart = await _seed.SeedPhoenixChartAsync(20);
        var passedChart = await _seed.SeedPhoenixChartAsync(19);

        var writer = BuildRepository();
        await Record(writer, userId, brokenChart, 812_345, isBroken: true);
        await Record(writer, userId, passedChart, 963_210, isBroken: false);
        await SeedStatsAsync(userId, brokenChart, 120.5);
        await SeedStatsAsync(userId, passedChart, 340.25);

        await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var stats = await ctx.Set<PhoenixRecordStatsEntity>().Where(s => s.UserId == userId)
            .Select(s => s.ChartId).ToArrayAsync();
        Assert.Equal(new[] { passedChart }, stats);
    }

    [Fact]
    public async Task CountingAgreesWithWhatTheCleanupThenRemoves()
    {
        var userId = await _seed.SeedUserAsync();
        var writer = BuildRepository();
        foreach (var level in new[] { 18, 19, 20 })
            await Record(writer, userId, await _seed.SeedPhoenixChartAsync(level), 700_000, isBroken: true);
        await Record(writer, userId, await _seed.SeedPhoenixChartAsync(21), 960_000, isBroken: false);

        var counted = await BuildRepository().CountBrokenRecords(MixEnum.Phoenix, userId);
        var removed = await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        Assert.Equal(3, counted);
        Assert.Equal(counted, removed);
        Assert.Equal(0, await BuildRepository().CountBrokenRecords(MixEnum.Phoenix, userId));
    }

    [Fact]
    public async Task AHandEnteredBreakIsNeverTouched()
    {
        // Manual data is the player's own submission, and the card's promise — turn the setting
        // back on, import again, they come back — is true of nothing a human typed. A CSV upload
        // counts as manual for the same reason (score-truth-model.md D9).
        var userId = await _seed.SeedUserAsync();
        var typed = await _seed.SeedPhoenixChartAsync(20);
        var uploaded = await _seed.SeedPhoenixChartAsync(21);
        var imported = await _seed.SeedPhoenixChartAsync(22);

        var writer = BuildRepository();
        await Record(writer, userId, typed, 812_345, isBroken: true, source: ScoreJournalEntry.ManualSource);
        await Record(writer, userId, uploaded, 700_000, isBroken: true, source: ScoreJournalEntry.CsvSource);
        await Record(writer, userId, imported, 654_321, isBroken: true,
            source: ScoreJournalEntry.OfficialImportSource);

        var counted = await BuildRepository().CountBrokenRecords(MixEnum.Phoenix, userId);
        var removed = await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        // The count is what the button prints, so it has to agree with the delete.
        Assert.Equal(1, counted);
        Assert.Equal(1, removed);
        var left = (await BuildRepository().GetRecordedScores(MixEnum.Phoenix, userId)).ToArray();
        Assert.Equal(new[] { typed, uploaded }.OrderBy(g => g), left.Select(s => s.ChartId).OrderBy(g => g));
    }

    [Fact]
    public async Task ABreakThatPredatesSourceCaptureIsLeftAlone()
    {
        // A null Source could be anything, including something a human typed years ago. Unknown
        // origin gets the same benefit of the doubt as a known-manual one.
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        await Record(BuildRepository(), userId, chartId, 812_345, isBroken: true, source: null);

        Assert.Equal(0, await BuildRepository().CountBrokenRecords(MixEnum.Phoenix, userId));
        Assert.Equal(0, await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId));
        Assert.Single(await BuildRepository().GetRecordedScores(MixEnum.Phoenix, userId));
    }

    [Fact]
    public async Task AnAccountWithNothingBrokenIsANoOp()
    {
        var userId = await _seed.SeedUserAsync();
        await Record(BuildRepository(), userId, await _seed.SeedPhoenixChartAsync(20), 960_000, isBroken: false);

        var removed = await BuildRepository().DeleteBrokenRecords(MixEnum.Phoenix, userId);

        Assert.Equal(0, removed);
        Assert.Single(await BuildRepository().GetRecordedScores(MixEnum.Phoenix, userId));
    }

    private static Task Record(EFPhoenixRecordsRepository repository, Guid userId, Guid chartId, int score,
        bool isBroken, MixEnum mix = MixEnum.Phoenix, string? source = ScoreJournalEntry.OfficialImportSource)
    {
        // Plate is null on anything that is not a pass (score-truth-model.md D8).
        return repository.UpdateBestAttempt(mix, userId, new RecordedPhoenixScore(chartId,
            PhoenixScore.From(score), isBroken ? null : PhoenixPlate.SuperbGame, isBroken, RecordedAt, source));
    }

    private async Task SeedStatsAsync(Guid userId, Guid chartId, double pumbility)
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        ctx.Set<PhoenixRecordStatsEntity>().Add(new PhoenixRecordStatsEntity
        {
            UserId = userId,
            ChartId = chartId,
            MixId = MixIds.For(MixEnum.Phoenix),
            Pumbility = pumbility,
            PumbilityPlus = pumbility
        });
        await ctx.SaveChangesAsync();
    }

    private async Task EnsurePhoenix2MixAsync()
    {
        await using var ctx = await _fixture.DbContextFactory.CreateDbContextAsync();
        var mixId = MixIds.For(MixEnum.Phoenix2);
        if (await ctx.Mix.AnyAsync(m => m.Id == mixId)) return;
        ctx.Mix.Add(new MixEntity { Id = mixId, Name = "Phoenix 2" });
        await ctx.SaveChangesAsync();
    }
}
