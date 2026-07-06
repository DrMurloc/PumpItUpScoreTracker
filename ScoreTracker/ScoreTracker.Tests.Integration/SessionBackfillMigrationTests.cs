using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Migrations;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Behavioral coverage for the BackfillRecentSessions data migration. The migration
///     itself always runs against an empty journal in fixtures, so the test executes its
///     exact production SQL against seeded rows: the last three 8-hour-gap clusters per
///     (user, mix) get stamped — keeping past-midnight sessions whole and splitting
///     double visits — older clusters stay day-bucketed, and already-stamped rows are
///     never touched.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class SessionBackfillMigrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;
    private readonly TestDataSeeder _seed;

    public SessionBackfillMigrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _seed = new TestDataSeeder(_fixture.DbContextFactory);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StampsTheLastThreeClustersKeepsMidnightSessionsWholeAndSplitsDoubleVisits()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = new EFScoreJournalRepository(_fixture.DbContextFactory);
        var preStamped = Guid.NewGuid();

        // Five unstamped clusters, oldest first. Clusters 1+2 share a calendar day with
        // an 11-hour gap (a real double visit); cluster 3 crosses midnight with a
        // 1-hour gap (one session).
        var morning = Now.AddDays(-30).AddHours(-2); // 10:00
        await Append(repo, userId, chart, morning, 900000);
        await Append(repo, userId, chart, morning.AddMinutes(30), 905000);
        await Append(repo, userId, chart, morning.AddHours(11), 910000); // 21:00 same day
        await Append(repo, userId, chart, Now.AddDays(-10).AddHours(11.5), 920000); // 23:30
        await Append(repo, userId, chart, Now.AddDays(-9).AddHours(-11.5), 925000); // 00:30 next day
        await Append(repo, userId, chart, Now.AddDays(-5), 930000);
        await Append(repo, userId, chart, Now.AddDays(-1), 940000);
        // An organically-stamped row — the migration must leave it alone.
        await Append(repo, userId, chart, Now, 950000, preStamped);

        await using (var context = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync(BackfillRecentSessions.Sql);
        }

        var (_, groups) = await repo.GetSessionGroups(MixEnum.Phoenix, userId, page: 1, pageSize: 10,
            CancellationToken.None);

        // Newest first: pre-stamped, cluster5, cluster4, cluster3 (midnight, whole),
        // then clusters 1+2 unstamped — merged into one day bucket.
        Assert.Equal(5, groups.Count);
        Assert.Equal(preStamped, groups[0].SessionId);
        Assert.NotNull(groups[1].SessionId);
        Assert.NotNull(groups[2].SessionId);
        Assert.NotNull(groups[3].SessionId);
        Assert.Equal(2, groups[3].Rows.Count); // the midnight session stayed whole
        Assert.Equal(4, groups.Where(g => g.SessionId != null).Select(g => g.SessionId).Distinct().Count());
        Assert.Null(groups[4].SessionId); // older clusters stay day-bucketed
        Assert.Equal(3, groups[4].Rows.Count);
    }

    private static Task Append(EFScoreJournalRepository repo, Guid userId, Guid chartId, DateTimeOffset at,
        int score, Guid? sessionId = null)
    {
        return repo.Append(new ScoreJournalEntry(at, ScoreJournalEntry.ManualSource, userId, chartId,
            PhoenixScore.From(score), PhoenixPlate.FairGame, false, MixEnum.Phoenix, sessionId),
            CancellationToken.None);
    }
}
