using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Domain;
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
    public async Task ImportsRunWithinEightHoursOfEachOtherPageAsOneSessionCarryingEveryId()
    {
        // The owner's case: an import per hour is one night, and a page of one session holds all
        // of it — the fold runs before the page is cut, so a night never straddles two pages.
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var imports = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var lastNight = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddHours(-2), 900000, imports[0], MixEnum.Phoenix2),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddHours(-1), 910000, imports[1], MixEnum.Phoenix2),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartA, Now, 950000, imports[2], MixEnum.Phoenix2), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddHours(-12), 880000, lastNight, MixEnum.Phoenix2),
            CancellationToken.None);
        await Imported(imports[0], userId, Now.AddHours(-2), MixEnum.Phoenix2);
        await Imported(imports[1], userId, Now.AddHours(-1), MixEnum.Phoenix2);
        await Imported(imports[2], userId, Now, MixEnum.Phoenix2);
        // Ten hours without an import before the first of tonight's: a different session.
        await Imported(lastNight, userId, Now.AddHours(-12), MixEnum.Phoenix2);

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 1, before: null,
            CancellationToken.None);

        Assert.Equal(2, total);
        var tonight = Assert.Single(groups);
        Assert.Equal(imports[2], tonight.SessionId);
        Assert.Equal(imports, tonight.SessionIds);
        Assert.Equal(3, tonight.Rows.Count);

        var (_, secondPage) = await repo.GetSessionGroups(userId, page: 2, pageSize: 1, before: null,
            CancellationToken.None);
        Assert.Equal(lastNight, Assert.Single(secondPage).SessionId);
    }

    [Fact]
    public async Task AnImportCarryingAnOldDatedPlayPullsNoOtherNightIn()
    {
        // The bug check's shape: an import holding a stage break the site dated weeks back. Grouped
        // by play time it spanned every night since; grouped by import time it is tonight, and the
        // old play simply rides inside it, as it always did.
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var tonight = Guid.NewGuid();
        var tenDaysAgo = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddDays(-27), 400000, tonight, isBroken: true),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now, 950000, tonight), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now.AddDays(-10), 900000, tenDaysAgo), CancellationToken.None);
        await Imported(tonight, userId, Now);
        await Imported(tenDaysAgo, userId, Now.AddDays(-10));

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10, before: null,
            CancellationToken.None);

        Assert.Equal(2, total);
        Assert.Equal(tonight, groups[0].SessionId);
        Assert.Equal(2, groups[0].Rows.Count);
        Assert.Equal(tenDaysAgo, groups[1].SessionId);
    }

    [Fact]
    public async Task AStoredSessionWithNoImportTimeIsNeverGrouped()
    {
        // A CSV upload, an API request, anything from before the session table: no import time, so
        // nothing to group by, and nothing is inferred from the plays' dates instead.
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddMinutes(-30), 900000, Guid.NewGuid()),
            CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now, 910000, Guid.NewGuid()), CancellationToken.None);

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10, before: null,
            CancellationToken.None);

        Assert.Equal(2, total);
        Assert.All(groups, g => Assert.Single(g.SessionIds));
    }

    [Fact]
    public async Task APreCaptureDayNeverGroupsWithTheImportAfterIt()
    {
        var userId = await _seed.SeedUserAsync();
        var chartA = await _seed.SeedChartAsync();
        var chartB = await _seed.SeedChartAsync();
        var session = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chartA, Now.AddHours(-3), 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chartB, Now, 910000, session), CancellationToken.None);
        await Imported(session, userId, Now);

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10, before: null,
            CancellationToken.None);

        Assert.Equal(2, total);
        Assert.Equal(session, groups[0].SessionId);
        Assert.Null(groups[1].SessionId);
        Assert.NotNull(groups[1].Day);
    }

    [Fact]
    public async Task TheBeforeFilterTestsASessionsLastPlay()
    {
        // A night that crossed midnight ended after it, so "before midnight" leaves the whole night
        // out rather than the half of it that happened to be earlier.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var midnight = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var beforeMidnight = Guid.NewGuid();
        var afterMidnight = Guid.NewGuid();
        var repo = BuildRepository();
        await repo.Append(Entry(userId, chart, midnight.AddHours(-1), 900000, beforeMidnight),
            CancellationToken.None);
        await repo.Append(Entry(userId, chart, midnight.AddHours(1), 910000, afterMidnight),
            CancellationToken.None);
        await Imported(beforeMidnight, userId, midnight.AddHours(-1));
        await Imported(afterMidnight, userId, midnight.AddHours(1));

        var (total, groups) = await repo.GetSessionGroups(userId, page: 1, pageSize: 10, before: midnight,
            CancellationToken.None);

        Assert.Equal(0, total);
        Assert.Empty(groups);
    }

    [Fact]
    public async Task AnyStoredIdFindsItsSessionFromAnywhereInTheHistory()
    {
        // A Discord card from last week links an import a dozen sessions back; the lookup finds the
        // night holding it without paging to it.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var oldNight = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var oldStart = Now.AddDays(-20);
        await repo.Append(Entry(userId, chart, oldStart, 880000, oldNight[0]), CancellationToken.None);
        await repo.Append(Entry(userId, chart, oldStart.AddMinutes(45), 890000, oldNight[1]),
            CancellationToken.None);
        await Imported(oldNight[0], userId, oldStart);
        await Imported(oldNight[1], userId, oldStart.AddMinutes(45));
        for (var day = 1; day <= 12; day++)
        {
            var later = Guid.NewGuid();
            await repo.Append(Entry(userId, chart, oldStart.AddDays(day), 890000 + day * 1000, later),
                CancellationToken.None);
            await Imported(later, userId, oldStart.AddDays(day));
        }

        var found = await repo.GetSessionGroupContaining(userId, oldNight[0], CancellationToken.None);
        var missing = await repo.GetSessionGroupContaining(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(oldNight[1], found!.SessionId);
        Assert.Equal(oldNight, found.SessionIds);
        Assert.Equal(2, found.Rows.Count);
        Assert.Null(missing);
    }

    /// <summary>The session row an import opens: when it ran, by the wall clock.</summary>
    private Task Imported(Guid sessionId, Guid userId, DateTimeOffset startedAt, MixEnum mix = MixEnum.Phoenix)
    {
        return new EFScoreSessionRepository(_fixture.DbContextFactory).Open(sessionId, userId, mix,
            ScoreJournalEntry.OfficialImportSource, null, null, startedAt, CancellationToken.None);
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
    public async Task ABestLandingOnAnotherPlaysKeyLeavesThatPlayAlone()
    {
        // The Phoenix 2 case that started this: a chart failed on the 12th and passed on the 14th
        // carries the 12th on its best card, because the card is stamped when the chart reaches
        // the list and never moves. The pass must not take over the fail's row — the fail happened.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var firstAttempt = Now.AddDays(-2);

        await repo.Append(Entry(userId, chart, firstAttempt, 900931, isBroken: true), CancellationToken.None);
        await repo.Append(Entry(userId, chart, firstAttempt, 955291), CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.True(row.IsBroken);
        Assert.Equal((PhoenixScore)900931, row.Score);
    }

    [Fact]
    public async Task ABestLandingOnAStageBreaksKeyLeavesTheStageBreakAlone()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();

        await repo.AppendObservations(new[] { StageBreak(userId, chart, Now, new JudgementCounts(244, 5, 2, 1, 110)) },
            CancellationToken.None);
        await repo.Append(Entry(userId, chart, Now, 955291, mix: MixEnum.Phoenix2), CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.True(row.IsStageBroken);
        Assert.False(row.IsBest);
        Assert.Null(row.Score);
    }

    [Fact]
    public async Task AppendReportsWhenAnotherPlayHoldsTheTime()
    {
        // The record handler journals the best at a time of its own when told, so the report has to
        // be exact: a new row and the same play again are both written; a different play is not.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var firstPlay = Now.AddDays(-6);

        Assert.Equal(JournalAppend.Written,
            await repo.Append(Entry(userId, chart, firstPlay, 983934), CancellationToken.None));
        Assert.Equal(JournalAppend.Written,
            await repo.Append(Entry(userId, chart, firstPlay, 983934), CancellationToken.None));
        Assert.Equal(JournalAppend.TimeHeldByAnotherPlay,
            await repo.Append(Entry(userId, chart, firstPlay, 999283), CancellationToken.None));

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.Equal(983934, (int)row.Score!.Value);
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
    public async Task AStageBreaksJudgementsNeverFillAnUnjudgedBestAtTheSameKey()
    {
        // The Phoenix 2 first-play stamp again, from the other side: an unjudged passing best
        // and a judged stage break can share a key while being two different plays. The fill
        // must not cross that line — otherwise the pass ends up wearing the break's partial
        // counts and its cause.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();

        await repo.Append(Entry(userId, chart, Now, 955291, mix: MixEnum.Phoenix2), CancellationToken.None);
        await repo.AppendObservations(
            new[]
            {
                StageBreak(userId, chart, Now, new JudgementCounts(806, 1, 0, 0, 4)) with
                {
                    Cause = new StageBreakCause(true, null, PhoenixLetterGrade.SSSPlus)
                }
            }, CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.False(row.IsStageBroken);
        Assert.Null(row.Judgements);
        Assert.False(row.Cause.IsNonLifebarBreak);
        Assert.Null(row.Cause.PassGrade);
    }

    [Fact]
    public async Task TheCauseRoundTripsWithTheJudgementsOnTheFillIn()
    {
        // The legitimate fill — best-list stage break first, judged twin later — carries the
        // cause in with the breakdown it was solved from.
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();

        await repo.AppendObservations(new[] { StageBreak(userId, chart, Now, null) }, CancellationToken.None);
        await repo.AppendObservations(
            new[]
            {
                StageBreak(userId, chart, Now, new JudgementCounts(806, 1, 0, 0, 4)) with
                {
                    Cause = new StageBreakCause(true, null, PhoenixLetterGrade.SSSPlus)
                }
            }, CancellationToken.None);

        var row = Assert.Single(await repo.GetChartHistories(userId, new[] { chart }, CancellationToken.None));
        Assert.True(row.IsStageBroken);
        Assert.True(row.Cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixLetterGrade.SSSPlus, row.Cause.PassGrade);
    }

    [Fact]
    public async Task JudgedStageBreaksReadsOnlyStageBreaks()
    {
        var userId = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();

        await repo.AppendObservations(new[]
        {
            Observation(userId, chart, Now.AddMinutes(-1), 910000) with
            {
                Judgements = new JudgementCounts(900, 40, 5, 2, 3)
            },
            StageBreak(userId, chart, Now, new JudgementCounts(806, 1, 0, 0, 4))
        }, CancellationToken.None);

        var rows = await repo.GetJudgedStageBreaks(userId, MixEnum.Phoenix2, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.True(row.IsStageBroken);
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

    [Fact]
    public async Task JudgedPlaysListJudgementCarryingScreensNewestFirstWithStageBreaksOut()
    {
        var userId = await _seed.SeedUserAsync();
        var stranger = await _seed.SeedUserAsync();
        var chart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        var judgements = new JudgementCounts(900, 50, 10, 5, 35, 300);
        // Oldest to newest: an unjudged play, a judged finished fail, a judged stage break,
        // and a judged clear -- plus a stranger's judged play that must never surface.
        await repo.Append(Entry(userId, chart, Now.AddMinutes(-30), 900000), CancellationToken.None);
        await repo.Append(Entry(userId, chart, Now.AddMinutes(-20), 480000, isBroken: true)
            with { Judgements = judgements }, CancellationToken.None);
        await repo.AppendObservations(new[]
        {
            Entry(userId, chart, Now.AddMinutes(-10), 0, isBroken: true)
                with { Judgements = new JudgementCounts(100, 5, 1, 0, 2), IsStageBroken = true, IsBest = false }
        }, CancellationToken.None);
        await repo.Append(Entry(userId, chart, Now, 960000) with { Judgements = judgements },
            CancellationToken.None);
        await repo.Append(Entry(stranger, chart, Now, 970000) with { Judgements = judgements },
            CancellationToken.None);

        var plays = await repo.GetJudgedPlays(userId, MixEnum.Phoenix, 10, CancellationToken.None);

        Assert.Equal(2, plays.Count);
        Assert.Equal(960000, (int)plays[0].Score!.Value);
        Assert.NotNull(plays[0].Judgements);
        // The finished fail stays: a broken run that reached the last note is a complete screen.
        Assert.True(plays[1].IsBroken);
        Assert.False(plays[1].IsStageBroken);
        Assert.All(plays, p => Assert.Equal(userId, p.UserId));

        var capped = await repo.GetJudgedPlays(userId, MixEnum.Phoenix, 1, CancellationToken.None);
        Assert.Equal(960000, (int)Assert.Single(capped).Score!.Value);
    }

    [Fact]
    public async Task ChartStageBreaksComeBackAcrossPlayersJudgedOnlyRightMixOnly()
    {
        var userA = await _seed.SeedUserAsync();
        var userB = await _seed.SeedUserAsync();
        var chartId = await _seed.SeedChartAsync();
        var otherChart = await _seed.SeedChartAsync();
        var repo = BuildRepository();
        // Two judged breaks on the chart (one of them a proven Pass), one break with no
        // judgements, one finished play, one judged break on another chart, and one judged
        // break on the same chart in the other mix — only the first two may come back.
        await repo.AppendObservations(new[]
        {
            Entry(userA, chartId, Now, 0, isBroken: true, mix: MixEnum.Phoenix2) with
            {
                IsStageBroken = true, IsBest = false,
                Judgements = new JudgementCounts(700, 2, 0, 0, 1),
                Cause = new StageBreakCause(true, null, null)
            },
            Entry(userB, chartId, Now.AddMinutes(1), 0, isBroken: true, mix: MixEnum.Phoenix2) with
            {
                IsStageBroken = true, IsBest = false,
                Judgements = new JudgementCounts(100, 0, 0, 5, 20)
            },
            Entry(userB, chartId, Now.AddMinutes(2), 0, isBroken: true, mix: MixEnum.Phoenix2) with
            {
                IsStageBroken = true, IsBest = false
            },
            Entry(userB, chartId, Now.AddMinutes(3), 950000, mix: MixEnum.Phoenix2) with
            {
                IsBest = false, Judgements = new JudgementCounts(800, 0, 0, 0, 0)
            },
            Entry(userB, otherChart, Now.AddMinutes(4), 0, isBroken: true, mix: MixEnum.Phoenix2) with
            {
                IsStageBroken = true, IsBest = false,
                Judgements = new JudgementCounts(50, 0, 0, 0, 0)
            },
            Entry(userB, chartId, Now.AddMinutes(5), 0, isBroken: true) with
            {
                IsStageBroken = true, IsBest = false,
                Judgements = new JudgementCounts(60, 0, 0, 0, 0)
            },
            // A broken run that FINISHED the chart — no rail position, counted by the end-cap.
            Entry(userA, chartId, Now.AddMinutes(6), 400000, isBroken: true, mix: MixEnum.Phoenix2) with
            {
                IsBest = false, Judgements = new JudgementCounts(700, 50, 30, 20, 100)
            }
        }, CancellationToken.None);

        var read = await repo.GetChartStageBreaks(MixEnum.Phoenix2, chartId, CancellationToken.None);
        var rows = read.Rows;

        Assert.Equal(1, read.Unplaced);
        Assert.Equal(1, read.FinishedFails);
        Assert.Equal(2, rows.Count);
        var passRow = Assert.Single(rows, r => r.IsNonLifebarBreak);
        Assert.Equal(userA, passRow.UserId);
        Assert.Equal(703, passRow.Judgements.NoteCount);
        var lifeRow = Assert.Single(rows, r => !r.IsNonLifebarBreak);
        Assert.Equal(125, lifeRow.Judgements.NoteCount);
    }

    private static ScoreJournalEntry Entry(Guid userId, Guid chartId, DateTimeOffset at, int score,
        Guid? sessionId = null, MixEnum mix = MixEnum.Phoenix, bool isBroken = false)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.ManualSource, userId, chartId,
            PhoenixScore.From(score), isBroken ? null : PhoenixPlate.FairGame, isBroken, mix, sessionId);
    }
}
