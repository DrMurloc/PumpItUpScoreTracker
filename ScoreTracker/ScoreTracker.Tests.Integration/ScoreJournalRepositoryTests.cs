using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ScoreJournalRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public ScoreJournalRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFScoreJournalRepository BuildRepository() => new(_fixture.DbContextFactory);

    [Fact]
    public async Task SessionGroupsPageNewestFirstWithPreCaptureRowsGroupedByDay()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var oldSession = Guid.NewGuid();
        var newSession = Guid.NewGuid();
        var repo = BuildRepository();
        // A legacy (pre-capture) row two days ago, an older session, and a newer session.
        await repo.Append(Entry(userId, chartA, Now.AddDays(-2), 900000, sessionId: null),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartA, Now.AddDays(-1), 920000, sessionId: oldSession),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddMinutes(-5), 910000, sessionId: newSession),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartA, Now, 950000, sessionId: newSession), CancellationToken.None);

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 2,
            CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Equal(2, groups.Count);
        Assert.Equal(newSession, groups[0].SessionId);
        Assert.Equal(2, groups[0].Rows.Count);
        Assert.Equal(oldSession, groups[1].SessionId);

        var (_, secondPage) = await repo.GetSessionGroups(userId, page: 2, pageSize: 2,
            CancellationToken.None);
        var legacy = Assert.Single(secondPage);
        Assert.Null(legacy.SessionId);
        Assert.NotNull(legacy.Day);
        Assert.Single(legacy.Rows);
    }

    [Fact]
    public async Task ChartHistoriesReturnRowsOldestFirstForTheRequestedChartsOnly()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddDays(-1), 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chartA, Now, 950000), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now, 800000), CancellationToken.None);

        var history = await repo.GetChartHistories(userId, new[] { chartA },
            CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].OccurredAt < history[1].OccurredAt);
        Assert.All(history, h => Assert.Equal(chartA, h.ChartId));
    }

    [Fact]
    public async Task GroupsInterleaveAcrossMixesNewestFirstEachCarryingItsMix()
    {
        // One continuous timeline (owner call): sessions and day buckets from every mix
        // sort together by recency; pre-capture day buckets stay separate per mix.
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var phoenixSession = Guid.NewGuid();
        var phoenix2Session = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddHours(-3), 900000, sessionId: phoenixSession),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddHours(-2), 910000, sessionId: phoenix2Session,
            mix: MixEnum.Phoenix2), CancellationToken.None);
        // Two pre-capture rows on the same calendar day, one per mix — separate buckets.
        await repo.Append(Entry(userId, chartA, Now.AddDays(-5), 880000), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddDays(-5).AddHours(1), 885000, mix: MixEnum.Phoenix2),
            CancellationToken.None);

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10,
            CancellationToken.None);

        Assert.Equal(4, total);
        Assert.Equal(phoenix2Session, groups[0].SessionId);
        Assert.Equal(MixEnum.Phoenix2, groups[0].Mix);
        Assert.Equal(phoenixSession, groups[1].SessionId);
        Assert.Equal(MixEnum.Phoenix, groups[1].Mix);
        Assert.Null(groups[2].SessionId);
        Assert.Equal(MixEnum.Phoenix2, groups[2].Mix);
        Assert.Single(groups[2].Rows);
        Assert.Null(groups[3].SessionId);
        Assert.Equal(MixEnum.Phoenix, groups[3].Mix);
        Assert.Single(groups[3].Rows);
    }

    private static ScoreJournalEntry Entry(Guid userId, Guid chartId, DateTimeOffset at, int score,
        Guid? sessionId = null, MixEnum mix = MixEnum.Phoenix)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.ManualSource, userId, chartId,
            PhoenixScore.From(score), PhoenixPlate.FairGame, false, mix, sessionId);
    }
}
