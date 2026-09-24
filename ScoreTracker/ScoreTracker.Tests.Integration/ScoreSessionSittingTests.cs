using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The session repository's sitting reads against a real database: which session is a player's
///     open sitting and what span of play times it holds, the arrival stamp, and the sweep's list.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ScoreSessionSittingTests : IAsyncLifetime
{
    private const string Source = "api:watcher-grab";
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public ScoreSessionSittingTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFScoreSessionRepository Sessions() => new(_fixture.DbContextFactory);
    private EFScoreJournalRepository Journal() => new(_fixture.DbContextFactory);

    private static ScoreJournalEntry Play(Guid userId, Guid chartId, DateTimeOffset at, Guid sessionId)
    {
        return new ScoreJournalEntry(at, Source, userId, chartId, PhoenixScore.From(950000),
            PhoenixPlate.SuperbGame, false, MixEnum.Rise, sessionId);
    }

    [Fact]
    public async Task AnOpenSittingSpansThePlayTimesItsJournalHolds()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var sitting = Guid.NewGuid();
        await Sessions().Open(sitting, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-5));
        await Journal().Append(Play(userId, chartA, Now.AddMinutes(-30), sitting), CancellationToken.None);
        await Journal().Append(Play(userId, chartB, Now.AddMinutes(-10), sitting), CancellationToken.None);

        var open = await Sessions().GetOpenSitting(userId, MixEnum.Rise, Now.AddMinutes(-15));

        Assert.NotNull(open);
        Assert.Equal(sitting, open!.Id);
        Assert.Equal(Now.AddMinutes(-30), open.FirstPlayedAt);
        Assert.Equal(Now.AddMinutes(-10), open.LastPlayedAt);
    }

    [Fact]
    public async Task TheLastPlayedTimeIsTheLatestPlayTheSittingsJournalHolds()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var sitting = Guid.NewGuid();
        await Sessions().Open(sitting, userId, MixEnum.Rise, Source, null, null, Now);
        await Journal().Append(Play(userId, chartA, Now.AddDays(-3), sitting), CancellationToken.None);
        await Journal().Append(Play(userId, chartB, Now.AddDays(-2), sitting), CancellationToken.None);

        Assert.Equal(Now.AddDays(-2), await Sessions().GetLastPlayedAt(userId, sitting));
        Assert.Null(await Sessions().GetLastPlayedAt(userId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ASittingWithNothingJournaledYetSpansItsStart()
    {
        var userId = await _seed.SeedUserAsync();
        var sitting = Guid.NewGuid();
        await Sessions().Open(sitting, userId, MixEnum.Rise, Source, null, null, Now);

        var open = await Sessions().GetOpenSitting(userId, MixEnum.Rise, Now.AddMinutes(-15));

        Assert.Equal(Now, open!.FirstPlayedAt);
        Assert.Equal(Now, open.LastPlayedAt);
    }

    [Fact]
    public async Task OnlyAnUnannouncedPlaysEndpointSessionStillArrivingIsOpen()
    {
        var userId = await _seed.SeedUserAsync();
        var quiet = Guid.NewGuid();
        var import = Guid.NewGuid();
        var announced = Guid.NewGuid();
        var otherMix = Guid.NewGuid();
        await Sessions().Open(quiet, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-40));
        await Sessions().Open(import, userId, MixEnum.Rise, ScoreJournalEntry.CsvSource, null, null, Now);
        await Sessions().Open(announced, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-1));
        await Sessions().MarkProcessed(announced, Now);
        await Sessions().Open(otherMix, userId, MixEnum.RiseArcade, Source, null, null, Now);

        Assert.Null(await Sessions().GetOpenSitting(userId, MixEnum.Rise, Now.AddMinutes(-15)));
    }

    [Fact]
    public async Task ASittingBeingAnnouncedIsNotOpenEvenThoughItsReplayJustTouchedIt()
    {
        var userId = await _seed.SeedUserAsync();
        var sitting = Guid.NewGuid();
        await Sessions().Open(sitting, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-20));
        // The replay writes the counts and its own time, and capture has not stamped it yet.
        await Sessions().SetCounts(sitting, Now, 2, 1);

        Assert.Null(await Sessions().GetOpenSitting(userId, MixEnum.Rise, Now.AddMinutes(-15)));
    }

    [Fact]
    public async Task AnArrivalStampOnlyEverMovesForward()
    {
        var userId = await _seed.SeedUserAsync();
        var sitting = Guid.NewGuid();
        await Sessions().Open(sitting, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-30));

        await Sessions().TouchArrival(sitting, Now);
        await Sessions().TouchArrival(sitting, Now.AddMinutes(-20));

        Assert.Equal(Now, (await Sessions().Get(sitting))!.LastActivityAt);
    }

    [Fact]
    public async Task TheSweepListsOnlyQuietSittingsNeitherReplayedNorAnnounced()
    {
        var userId = await _seed.SeedUserAsync();
        var lost = Guid.NewGuid();
        var replayed = Guid.NewGuid();
        var announced = Guid.NewGuid();
        var live = Guid.NewGuid();
        var import = Guid.NewGuid();
        await Sessions().Open(lost, userId, MixEnum.Rise, Source, null, null, Now.AddHours(-2));
        await Sessions().Open(replayed, userId, MixEnum.Rise, Source, null, null, Now.AddHours(-2));
        await Sessions().SetCounts(replayed, Now.AddHours(-2), 3, 1);
        await Sessions().Open(announced, userId, MixEnum.Rise, Source, null, null, Now.AddHours(-2));
        await Sessions().MarkProcessed(announced, Now.AddHours(-1));
        await Sessions().Open(live, userId, MixEnum.Rise, Source, null, null, Now.AddMinutes(-3));
        await Sessions().Open(import, userId, MixEnum.Rise, ScoreJournalEntry.OfficialImportSource, null, null,
            Now.AddHours(-2));

        var overdue = await Sessions().ListOverdueSittings(Now.AddMinutes(-20), 25);

        Assert.Equal(lost, Assert.Single(overdue).Id);
    }
}
