using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The counting rule against a real record table (docs/design/seasons.md D15, §4.1): the writer
///     composing the policy with the repository, so a seasonal row is proven to land beside the
///     all-time one rather than merely asked for on a mock.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class SeasonalBestWriterTests : IAsyncLifetime
{
    private const short Fall2026 = 20264;
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);
    private static readonly DateTimeOffset InFall = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = new(2025, 5, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public SeasonalBestWriterTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFPhoenixRecordsRepository Records() =>
        new(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions()), Mock.Of<IChartRepository>(),
            new EFXXChartAttemptRepository(_fixture.DbContextFactory), Mock.Of<IMediator>(),
            Mock.Of<IPlayerStatsReader>(), new PeerScoreStore(_fixture.DbContextFactory));

    private static SeasonalBestWriter Writer(EFPhoenixRecordsRepository records)
    {
        var offset = TimeSpan.FromHours(-5);
        var seasons = new Mock<ISeasonReader>();
        seasons.Setup(s => s.GetSeasons(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new SeasonRecord(Fall, "Fall 2026", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0), offset),
                new DateTimeOffset(new DateTime(2026, 12, 31, 23, 59, 59), offset), null, false)
        });
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(InFall);
        return new SeasonalBestWriter(records, seasons.Object, clock.Object);
    }

    private async Task<int> CountRows(short season)
    {
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        return await database.Set<PhoenixRecordEntity>().IgnoreQueryFilters()
            .CountAsync(e => e.SeasonId == season);
    }

    [Fact]
    public async Task AnInWindowRunLandsASeasonRowBesideTheAllTimeOneAndLaterRunsRaiseIt()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        var records = Records();
        await records.UpdateBestAttempt(MixEnum.Phoenix, userId,
            new RecordedPhoenixScore(chartId, PhoenixScore.From(985_000), PhoenixPlate.SuperbGame, false, InFall,
                ScoreJournalEntry.OfficialImportSource));
        var writer = Writer(records);

        // Well below the all-time best, so the import never offered it to the record handler — and
        // it is this season's best all the same.
        await writer.Write(MixEnum.Phoenix, userId, ScoreJournalEntry.OfficialImportSource,
            new[] { Candidate(chartId, 900_000, InFall) }, CancellationToken.None);

        Assert.Equal(900_000, (int)(await records.GetRecordedScore(MixEnum.Phoenix, userId, chartId, Fall))!.Score!.Value);
        Assert.Equal(985_000, (int)(await records.GetRecordedScore(MixEnum.Phoenix, userId, chartId))!.Score!.Value);

        await writer.Write(MixEnum.Phoenix, userId, ScoreJournalEntry.OfficialImportSource,
            new[] { Candidate(chartId, 930_000, InFall.AddHours(1)) }, CancellationToken.None);

        Assert.Equal(930_000, (int)(await records.GetRecordedScore(MixEnum.Phoenix, userId, chartId, Fall))!.Score!.Value);
        // One row per season per chart, not one per run.
        Assert.Equal(1, await CountRows(Fall2026));
    }

    [Fact]
    public async Task AnOldCardFromAFirstEverImporterWritesNoSeasonRow()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedPhoenixChartAsync(20);
        var records = Records();

        await Writer(records).Write(MixEnum.Phoenix, userId, ScoreJournalEntry.OfficialImportSource,
            new[] { Candidate(chartId, 985_000, LongAgo) }, CancellationToken.None);

        Assert.Equal(0, await CountRows(Fall2026));
    }

    private static SeasonalBestWriter.Candidate Candidate(Guid chartId, int score, DateTimeOffset at,
        bool raisedExistingRecord = false)
    {
        return new SeasonalBestWriter.Candidate(
            new RecordedPhoenixScore(chartId, PhoenixScore.From(score), PhoenixPlate.SuperbGame, false, at,
                ScoreJournalEntry.OfficialImportSource), raisedExistingRecord);
    }
}
