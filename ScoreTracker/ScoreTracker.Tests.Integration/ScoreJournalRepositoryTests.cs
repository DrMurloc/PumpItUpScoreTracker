using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 2, before: null,
            CancellationToken.None);

        Assert.Equal(3, total);
        Assert.Equal(2, groups.Count);
        Assert.Equal(newSession, groups[0].SessionId);
        Assert.Equal(2, groups[0].Rows.Count);
        Assert.Equal(oldSession, groups[1].SessionId);

        var (_, secondPage) = await repo.GetSessionGroups(userId, page: 2, pageSize: 2, before: null,
            CancellationToken.None);
        var legacy = Assert.Single(secondPage);
        Assert.Null(legacy.SessionId);
        Assert.NotNull(legacy.Day);
        Assert.Single(legacy.Rows);
    }

    [Fact]
    public async Task AppendRoundTripsTheJudgementBreakdown()
    {
        var userId = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var judgements = new JudgementCounts(939, 6, 2, 2, 1);
        await repo.Append(Entry(userId, chartId, Now.AddMinutes(-1), 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chartId, Now, 991725) with { Judgements = judgements },
            CancellationToken.None);

        var history = await repo.GetChartHistories(userId, new[] { chartId }, CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Null(history[0].Judgements);
        Assert.Equal(judgements, history[1].Judgements);
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

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10, before: null,
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

    [Fact]
    public async Task ReimportingTheSameWindowLeavesOneRowPerPlay()
    {
        // A journal row is one play, keyed by the site's stamped play time. The import
        // deliberately re-reads past its cutoff, so the same recently-played window arrives
        // again on the next run and must not pile up.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var plays = new[]
        {
            Observation(userId, chart, Now.AddMinutes(-10), 880000),
            Observation(userId, chart, Now.AddMinutes(-5), 910000)
        };

        await repo.AppendObservations(plays, CancellationToken.None);
        await repo.AppendObservations(plays, CancellationToken.None);

        var history = await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.False(r.IsBest));
    }

    [Fact]
    public async Task TheBestRaisesTheObservationOfTheSamePlayInsteadOfDuplicatingIt()
    {
        // One import sees the play twice — once in recently-played, once on the best list as
        // the record change. Both carry the site's play time, so they are one row.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var playedAt = Now.AddMinutes(-5);
        var sessionId = Guid.NewGuid();

        await repo.AppendObservations(new[] { Observation(userId, chart, playedAt, 910000) },
            CancellationToken.None);
        await repo.Append(Entry(userId, chart, playedAt, 910000, sessionId), CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart },
            CancellationToken.None));
        Assert.True(row.IsBest);
        // The observation had no session; the best supplies it.
        Assert.Equal(sessionId, row.SessionId);
    }

    [Fact]
    public async Task AnObservationNeverDemotesAPlayAlreadyRecordedAsTheBest()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var playedAt = Now.AddMinutes(-5);

        await repo.Append(Entry(userId, chart, playedAt, 910000), CancellationToken.None);
        await repo.AppendObservations(new[] { Observation(userId, chart, playedAt, 910000) },
            CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart },
            CancellationToken.None));
        Assert.True(row.IsBest);
    }

    [Fact]
    public async Task AStageBreakRoundTripsFlaggedScorelessAndNeverBest()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var judgements = new JudgementCounts(134, 2, 0, 0, 70);

        await repo.AppendObservations(new[] { StageBreak(userId, chart, Now, judgements) },
            CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.True(row.IsStageBroken);
        Assert.True(row.IsBroken);
        Assert.False(row.IsBest);
        Assert.Null(row.Score);
        Assert.Null(row.Plate);
        Assert.Equal(judgements, row.Judgements);
    }

    [Fact]
    public async Task TheJudgedTwinOfAStageBreakWinsWhicheverOrderTheTwoArrive()
    {
        // The same play reaches us twice: the best list keeps a stage break as a chart's first
        // attempt (no breakdown), the recent window still holds the play (with one). One row,
        // carrying the breakdown, whether they land in one batch or across two imports.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var judgements = new JudgementCounts(334, 7, 0, 0, 60);
        var fromList = StageBreak(userId, chart, Now, null);
        var fromWindow = StageBreak(userId, chart, Now, judgements);

        // Unjudged first inside one batch.
        await repo.AppendObservations(new[] { fromList, fromWindow }, CancellationToken.None);
        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.Equal(judgements, row.Judgements);

        // Unjudged already stored, judged arrives on a later import.
        var later = await _seed.SeedChartAsync();
        await repo.AppendObservations(new[] { StageBreak(userId, later, Now, null) }, CancellationToken.None);
        await repo.AppendObservations(new[] { StageBreak(userId, later, Now, judgements) }, CancellationToken.None);
        var filled = Assert.Single(await repo.GetChartHistories(userId, new[] { later }, CancellationToken.None));
        Assert.Equal(judgements, filled.Judgements);
        Assert.True(filled.IsStageBroken);
    }

    [Fact]
    public async Task TheComboRoundTripsWithTheBreakdownOnBothWritePaths()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var judgements = new JudgementCounts(939, 6, 2, 2, 1, 947);

        await repo.Append(Entry(userId, chart, Now.AddMinutes(-1), 991725) with { Judgements = judgements },
            CancellationToken.None);
        await repo.AppendObservations(new[] { Observation(userId, chart, Now, 990000) with { Judgements = judgements } },
            CancellationToken.None);

        var history = await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal(947, h.Judgements!.MaxCombo));
    }

    private static ScoreJournalEntry Observation(Guid userId, Guid chartId, DateTimeOffset at, int score)
    {
        return Entry(userId, chartId, at, score) with { IsBest = false };
    }

    private static ScoreJournalEntry StageBreak(Guid userId, Guid chartId, DateTimeOffset at,
        JudgementCounts? judgements)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, userId, chartId, null, null,
            true, MixEnum.Phoenix2, null, judgements, false, IsStageBroken: true);
    }

    // The partner API's read. Keyset rather than offset because the journal is appended to while a
    // caller walks it — these tests pin the page boundary, which is where an offset would repeat or
    // skip a row.
    [Fact]
    public async Task JournalPageWalksNewestFirstWithoutRepeatingAcrossTheBoundary()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        for (var i = 0; i < 5; i++)
            await repo.Append(Entry(userId, chart, Now.AddMinutes(-i), 900000 + i), CancellationToken.None);

        var first = await repo.GetJournalPage(userId, MixEnum.Phoenix, null, null, null, 2,
            CancellationToken.None);
        var last = first[^1];
        var second = await repo.GetJournalPage(userId, MixEnum.Phoenix, last.OccurredAt, last.ChartId, null, 2,
            CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(Now, first[0].OccurredAt);
        Assert.Equal(2, second.Count);
        Assert.All(second, e => Assert.True(e.OccurredAt < last.OccurredAt));
        Assert.Empty(first.Select(e => e.OccurredAt).Intersect(second.Select(e => e.OccurredAt)));
    }

    // Two plays can share an instant across charts; without the chart-id tiebreaker the cursor
    // would either drop one or loop on it forever.
    [Fact]
    public async Task RowsSharingAnInstantAreSeparatedByTheChartIdTiebreaker()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now, 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now, 910000), CancellationToken.None);

        var first = await repo.GetJournalPage(userId, MixEnum.Phoenix, null, null, null, 1,
            CancellationToken.None);
        var second = await repo.GetJournalPage(userId, MixEnum.Phoenix, first[0].OccurredAt, first[0].ChartId,
            null, 5, CancellationToken.None);

        Assert.Single(second);
        Assert.NotEqual(first[0].ChartId, second[0].ChartId);
    }

    [Fact]
    public async Task JournalPageIsScopedToOneMixAndHonoursSince()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chart, Now.AddDays(-10), 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chart, Now, 950000), CancellationToken.None);
        await repo.Append(Entry(userId, chart, Now, 960000, mix: MixEnum.Phoenix2), CancellationToken.None);

        var phoenix = await repo.GetJournalPage(userId, MixEnum.Phoenix, null, null, Now.AddDays(-1), 50,
            CancellationToken.None);

        Assert.Single(phoenix);
        Assert.All(phoenix, e => Assert.Equal(MixEnum.Phoenix, e.Mix));
    }

    [Fact]
    public async Task TheLimboBoardTakesEachPlayersLowestPassAscending()
    {
        var low = await _seed.SeedUserAsync("LOWBALLER");
        var higher = await _seed.SeedUserAsync("TRIER");
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        // The lowballer's own best is journaled too — the board must take their MIN, not their best.
        await repo.Append(Entry(low, chart, Now.AddDays(-3), 962000), CancellationToken.None);
        await repo.AppendObservations(new[] { Entry(low, chart, Now.AddDays(-1), 312004) },
            CancellationToken.None);
        await repo.Append(Entry(higher, chart, Now, 640500), CancellationToken.None);

        var board = await repo.GetLowestPassingPlays(MixEnum.Phoenix, chart, 100, CancellationToken.None);

        Assert.Equal(2, board.Count);
        Assert.Equal(312004, (int)board[0].Score);
        Assert.Equal("LOWBALLER", board[0].UserName.ToString());
        Assert.Equal(640500, (int)board[1].Score);
    }

    [Fact]
    public async Task TheLimboBoardExcludesBreaksAndPrivatePlayers()
    {
        var hidden = await _seed.SeedUserAsync("HIDDEN", isPublic: false);
        var breaker = await _seed.SeedUserAsync("BREAKER");
        var clearer = await _seed.SeedUserAsync("CLEARER");
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        await repo.AppendObservations(new[] { Entry(hidden, chart, Now, 120000) }, CancellationToken.None);
        // Failing with a low score is not the achievement — surviving with one is (D4).
        await repo.AppendObservations(new[] { Entry(breaker, chart, Now, 140000, isBroken: true) },
            CancellationToken.None);
        await repo.AppendObservations(new[] { Entry(clearer, chart, Now, 480000) }, CancellationToken.None);

        var board = await repo.GetLowestPassingPlays(MixEnum.Phoenix, chart, 100, CancellationToken.None);

        var only = Assert.Single(board);
        Assert.Equal("CLEARER", only.UserName.ToString());
    }

    [Fact]
    public async Task TheLimboBoardIsMixScopedAndCapped()
    {
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        foreach (var score in new[] { 500000, 400000, 300000 })
            await repo.AppendObservations(
                new[] { Entry(await _seed.SeedUserAsync(), chart, Now, score) }, CancellationToken.None);
        // Same chart id, other mix: a flagged chart on Phoenix 2 must not serve Phoenix's rows.
        await repo.AppendObservations(
            new[] { Entry(await _seed.SeedUserAsync(), chart, Now, 100000, mix: MixEnum.Phoenix2) },
            CancellationToken.None);

        var capped = await repo.GetLowestPassingPlays(MixEnum.Phoenix, chart, 2, CancellationToken.None);
        var otherMix = await repo.GetLowestPassingPlays(MixEnum.Phoenix2, chart, 100, CancellationToken.None);

        Assert.Equal(new[] { 300000, 400000 }, capped.Select(r => (int)r.Score).ToArray());
        Assert.Equal(100000, (int)Assert.Single(otherMix).Score);
    }

    /// <summary>
    ///     The count is per chart within one mix, and a chart nobody journaled is absent rather
    ///     than zero. Mix scoping matters more than it looks: a returning song carries one
    ///     ChartId across Phoenix and Phoenix 2, so an unscoped count would add the two eras
    ///     together on exactly the charts a player is most likely to have played in both.
    /// </summary>
    [Fact]
    public async Task ChartPlayCountsGroupPerChartWithinTheMix()
    {
        var userId = await _seed.SeedUserAsync();
        var stranger = await _seed.SeedUserAsync();
        var played = await _seed.SeedChartAsync();
        var once = await _seed.SeedChartAsync();
        var untouched = await _seed.SeedChartAsync();
        var repo = BuildRepository();

        await repo.Append(Entry(userId, played, Now.AddDays(-2), 900000, mix: MixEnum.Phoenix2),
            CancellationToken.None);
        await repo.Append(Entry(userId, played, Now.AddDays(-1), 920000, mix: MixEnum.Phoenix2),
            CancellationToken.None);
        await repo.Append(Entry(userId, played, Now, 950000, mix: MixEnum.Phoenix2), CancellationToken.None);
        await repo.Append(Entry(userId, once, Now, 910000, mix: MixEnum.Phoenix2), CancellationToken.None);
        // Same chart, other era — must not be counted into Phoenix 2's total.
        await repo.Append(Entry(userId, played, Now, 880000), CancellationToken.None);
        // Somebody else's plays on the same chart.
        await repo.Append(Entry(stranger, played, Now, 870000, mix: MixEnum.Phoenix2), CancellationToken.None);

        var counts = await repo.GetChartPlayCounts(userId, MixEnum.Phoenix2, CancellationToken.None);

        Assert.Equal(3, counts[played]);
        Assert.Equal(1, counts[once]);
        Assert.DoesNotContain(untouched, counts.Keys);
        Assert.Equal(2, counts.Count);
    }

    private static ScoreJournalEntry Entry(Guid userId, Guid chartId, DateTimeOffset at, int score,
        Guid? sessionId = null, MixEnum mix = MixEnum.Phoenix, bool isBroken = false)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.ManualSource, userId, chartId,
            PhoenixScore.From(score), isBroken ? null : PhoenixPlate.FairGame, isBroken, mix, sessionId);
    }
}
