using ScoreTracker.Domain.Records;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Infrastructure;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The pipeline's two repositories against a real migrated database. The replace-on-upsert
///     semantics and the state walk are what the saga leans its money controls on, and a mock
///     records that the right method was called — never that the SQL honoured the unique key.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class TranslationPipelineRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 6, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public TranslationPipelineRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFTranslationRequestRepository Requests => new(_fixture.DbContextFactory);
    private EFTranslationBatchRepository Batches => new(_fixture.DbContextFactory);

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AReQueueReplacesTheRowInsteadOfGrowingAHistory()
    {
        await Requests.Upsert("chart-comment:a", "first words", Now);
        var first = (await Requests.NextIn(TranslationState.Pending, 10)).Single();
        await Requests.CompletePivot(first.Id, "ko", "{}", Now.AddHours(1));

        await Requests.Upsert("chart-comment:a", "edited words", Now.AddHours(2));

        var rows = await Requests.NextIn(TranslationState.Pending, 10);
        var row = Assert.Single(rows);
        Assert.Equal(first.Id, row.Id);
        Assert.Equal("edited words", row.Text);
        Assert.Null(row.SourceLanguage);
        Assert.Null(row.PivotJson);
        Assert.Equal(0, await Requests.CountIn(TranslationState.PivotDone));
    }

    [Fact]
    public async Task TheQueueIsOldestFirstBecauseThatIsStarvationFree()
    {
        await Requests.Upsert("chart-comment:new", "newest", Now.AddHours(2));
        await Requests.Upsert("chart-comment:old", "oldest", Now);
        await Requests.Upsert("chart-comment:mid", "middle", Now.AddHours(1));

        var taken = await Requests.NextIn(TranslationState.Pending, 2);

        Assert.Equal(new[] { "chart-comment:old", "chart-comment:mid" },
            taken.Select(t => t.SourceKey).ToArray());
    }

    [Fact]
    public async Task TheStateWalkRoundTripsThroughABatch()
    {
        await Requests.Upsert("chart-comment:a", "hola ⟦1⟧", Now);
        var work = (await Requests.NextIn(TranslationState.Pending, 1)).Single();
        var batchId = Guid.NewGuid();

        await Requests.MarkSubmitted(new[] { work.Id }, batchId, TranslationState.PivotSubmitted, Now);
        var inBatch = Assert.Single(await Requests.InBatch(batchId));
        Assert.Equal(work.Id, inBatch.Id);

        await Requests.CompletePivot(work.Id, "es", """{"english":"hi ⟦1⟧"}""", Now.AddHours(1));
        var pivoted = Assert.Single(await Requests.NextIn(TranslationState.PivotDone, 10));
        Assert.Equal("es", pivoted.SourceLanguage);
        Assert.Empty(await Requests.InBatch(batchId));

        await Requests.CompleteTranslation(work.Id, Now.AddHours(2));
        Assert.Equal(1, await Requests.CountIn(TranslationState.Translated));

        Assert.Equal(1, await Requests.RequeueTranslated(Now.AddHours(3)));
        var requeued = Assert.Single(await Requests.NextIn(TranslationState.Pending, 10));
        Assert.Null(requeued.PivotJson);
    }

    [Fact]
    public async Task FailureKeepsItsReasonAndSurfacesInRecentFailures()
    {
        await Requests.Upsert("chart-comment:a", "text", Now);
        var work = (await Requests.NextIn(TranslationState.Pending, 1)).Single();

        await Requests.Fail(work.Id, "the pivot lost marker ⟦1⟧", Now.AddHours(1));

        var failure = Assert.Single(await Requests.RecentFailures(5));
        Assert.Equal("the pivot lost marker ⟦1⟧", failure.FailureReason);
    }

    [Fact]
    public async Task DiscardRemovesWhateverStateTheRowsWereIn()
    {
        await Requests.Upsert("chart-comment:a", "queued", Now);
        await Requests.Upsert("chart-comment:b", "translated", Now);
        var b = (await Requests.NextIn(TranslationState.Pending, 10)).Single(w => w.SourceKey.EndsWith(":b"));
        await Requests.CompleteTranslation(b.Id, Now);

        await Requests.Discard(new[] { "chart-comment:a", "chart-comment:b" });

        Assert.Equal(0, await Requests.CountIn(TranslationState.Pending));
        Assert.Equal(0, await Requests.CountIn(TranslationState.Translated));
    }

    [Fact]
    public async Task TheBatchLedgerPricesTheRollingWindowFromRecordedFact()
    {
        var open = new TranslationBatchInfo(Guid.NewGuid(), "pb-open", TranslationState.PivotSubmitted, Now);
        var oldDone = new TranslationBatchInfo(Guid.NewGuid(), "pb-old", TranslationState.PivotSubmitted,
            Now.AddDays(-40));
        var recentDone = new TranslationBatchInfo(Guid.NewGuid(), "pb-recent", TranslationState.FanOutSubmitted,
            Now.AddDays(-2));
        await Batches.Record(open, 3);
        await Batches.Record(oldDone, 5);
        await Batches.Record(recentDone, 5);
        await Batches.Complete(oldDone.Id, new LanguageModelUsage(1000, 500), 5.00m, Now.AddDays(-40));
        await Batches.Complete(recentDone.Id, new LanguageModelUsage(2000, 900), 0.75m, Now.AddDays(-2));

        Assert.Equal(open.ProviderBatchId, Assert.Single(await Batches.Open()).ProviderBatchId);
        // Only the completed batch inside the window counts — open work is estimated elsewhere,
        // and the 40-day-old spend has rolled off.
        Assert.Equal(0.75m, await Batches.SpendSince(Now.AddDays(-30)));
        Assert.Equal(Now, await Batches.LastSubmittedAt());
        Assert.Equal(Now.AddDays(-2), await Batches.LastCollectedAt());
    }
}
