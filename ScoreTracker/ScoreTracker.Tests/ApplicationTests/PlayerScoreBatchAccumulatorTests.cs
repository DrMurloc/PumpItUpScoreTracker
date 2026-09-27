using System;
using System.Linq;
using ScoreTracker.ScoreLedger.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerScoreBatchAccumulatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void SubmissionsWithinTheGapShareOneSession()
    {
        var batcher = new PlayerScoreBatchAccumulator();

        var first = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var second = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource,
            Now + TimeSpan.FromHours(7));

        Assert.Equal(first.Id, second.Id);
        Assert.True(first.IsNew);
        Assert.False(second.IsNew);
    }

    [Fact]
    public void ActivityKeepsExtendingTheWindowPastTheOriginalGap()
    {
        // The gap is measured from the LAST activity, not the session start — a long
        // arcade session with steady entries stays one session.
        var batcher = new PlayerScoreBatchAccumulator();

        var first = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var second = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource,
            Now + TimeSpan.FromHours(6));
        var third = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource,
            Now + TimeSpan.FromHours(12));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(second.Id, third.Id);
    }

    [Fact]
    public void GapElapsedMintsANewSession()
    {
        var batcher = new PlayerScoreBatchAccumulator();

        var first = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var second = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource,
            Now + TimeSpan.FromHours(9));

        Assert.NotEqual(first.Id, second.Id);
        // Both minted an id, so both report it — that is what makes the session recordable.
        Assert.True(first.IsNew);
        Assert.True(second.IsNew);
    }

    [Fact]
    public void SourcesAndMixesTrackSeparateSessions()
    {
        var batcher = new PlayerScoreBatchAccumulator();

        var manual = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var import = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.OfficialImportSource,
            Now);
        var phoenix2 = batcher.GetOrExtendSession(MixEnum.Phoenix2, UserId, ScoreJournalEntry.ManualSource, Now);

        Assert.NotEqual(manual.Id, import.Id);
        Assert.NotEqual(manual.Id, phoenix2.Id);
    }

    [Fact]
    public void ExplicitRunIdTakesOverTheEnvelopeAndSubsequentCallsReuseIt()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        var runId = Guid.NewGuid();

        var explicitId = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId,
            ScoreJournalEntry.OfficialImportSource, Now, runId);
        var implicitFollowUp = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId,
            ScoreJournalEntry.OfficialImportSource, Now + TimeSpan.FromMinutes(5));

        Assert.Equal(runId, explicitId.Id);
        Assert.Equal(runId, implicitFollowUp.Id);
        // An explicit id belongs to a caller that already recorded the session itself.
        Assert.False(explicitId.IsNew);
    }

    [Fact]
    public void TakenBatchCarriesTheMostRecentSessionId()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var fireAt = Now.UtcDateTime;

        batcher.AddToBatch(MixEnum.Phoenix, UserId, fireAt, Guid.NewGuid(), true, null, sessionA);
        batcher.AddToBatch(MixEnum.Phoenix, UserId, fireAt, Guid.NewGuid(), true, null, sessionB);
        var batch = batcher.TakeBatch(MixEnum.Phoenix, UserId);

        Assert.NotNull(batch);
        Assert.Equal(sessionB, batch!.SessionId);
        Assert.Equal(2, batch.NewChartIds.Length);
    }

    [Fact]
    public void TakeDueBatchesTakesOnlyWhatIsDueAndRemovesIt()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        var dueUser = Guid.NewGuid();
        var waitingUser = Guid.NewGuid();
        batcher.AddToBatch(MixEnum.Phoenix, dueUser, Now.UtcDateTime.AddMinutes(-5), Guid.NewGuid(), true,
            null, Guid.NewGuid());
        batcher.AddToBatch(MixEnum.Phoenix, waitingUser, Now.UtcDateTime.AddMinutes(5), Guid.NewGuid(), true,
            null, Guid.NewGuid());

        var taken = batcher.TakeDueBatches(Now.UtcDateTime);

        Assert.Equal(new[] { dueUser }, taken.Select(t => t.UserId));
        // Taken means gone: a second sweep, or the scheduled drain arriving late, finds nothing.
        Assert.Empty(batcher.TakeDueBatches(Now.UtcDateTime));
        Assert.Null(batcher.TakeBatch(MixEnum.Phoenix, dueUser));
        Assert.NotNull(batcher.GetFireAt(MixEnum.Phoenix, waitingUser));
    }

    /// <summary>
    ///     ⚠ AddToBatch publishes the state into the dictionary before it takes the gate that
    ///     stamps FireAt, so a sweep can observe a batch that has no deadline yet. Treating
    ///     default(DateTime) as a date in the past seizes a batch that is microseconds old and
    ///     announces one score mid-set, debounce defeated.
    /// </summary>
    [Fact]
    public void ABatchWithNoDeadlineYetIsNotDue()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, default, Guid.NewGuid(), true, null, Guid.NewGuid());

        Assert.Empty(batcher.TakeDueBatches(Now.UtcDateTime));
        Assert.NotNull(batcher.TakeBatch(MixEnum.Phoenix, UserId));
    }

    [Fact]
    public void TakeDueBatchesKeepsEachMixSeparate()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime.AddMinutes(-5), Guid.NewGuid(), true,
            null, Guid.NewGuid());
        batcher.AddToBatch(MixEnum.Phoenix2, UserId, Now.UtcDateTime.AddMinutes(5), Guid.NewGuid(), true,
            null, Guid.NewGuid());

        var taken = batcher.TakeDueBatches(Now.UtcDateTime);

        Assert.Equal(new[] { MixEnum.Phoenix }, taken.Select(t => t.Batch.Mix));
    }

    [Fact]
    public void AChartThatUpscoredAndThenPassedInOneBatchIsANewPass()
    {
        // A break raised to a higher break, then passed: without dropping the upscore the chart sat in
        // both sets and was announced as an upscore.
        var batcher = new PlayerScoreBatchAccumulator();
        var chart = Guid.NewGuid();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime, chart, false, 900000, Guid.NewGuid());
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime, chart, true, null, Guid.NewGuid());

        var batch = batcher.TakeBatch(MixEnum.Phoenix, UserId)!;

        Assert.Equal(new[] { chart }, batch.NewChartIds);
        Assert.Empty(batch.UpscoredChartIds);
    }

    [Fact]
    public void AChartTypedInTwiceKeepsTheScoreItHadBeforeTheBatch()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        var chart = Guid.NewGuid();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime, chart, false, 900000, Guid.NewGuid());
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime, chart, false, 930000, Guid.NewGuid());

        Assert.Equal(900000, batcher.TakeBatch(MixEnum.Phoenix, UserId)!.UpscoredChartIds[chart]);
    }

    [Fact]
    public void MakingABatchDueBringsItsDeadlineForwardAndLeavesItInPlace()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime.AddMinutes(2), Guid.NewGuid(), true, null,
            Guid.NewGuid());

        Assert.True(batcher.MakeDue(MixEnum.Phoenix, UserId, Now.UtcDateTime));

        Assert.Equal(Now.UtcDateTime, batcher.GetFireAt(MixEnum.Phoenix, UserId));
        Assert.NotNull(batcher.TakeBatch(MixEnum.Phoenix, UserId));
    }

    [Fact]
    public void MakingABatchDueNeverPushesItsDeadlineLater()
    {
        var batcher = new PlayerScoreBatchAccumulator();
        batcher.AddToBatch(MixEnum.Phoenix, UserId, Now.UtcDateTime, Guid.NewGuid(), true, null, Guid.NewGuid());

        batcher.MakeDue(MixEnum.Phoenix, UserId, Now.UtcDateTime.AddMinutes(1));

        Assert.Equal(Now.UtcDateTime, batcher.GetFireAt(MixEnum.Phoenix, UserId));
    }

    [Fact]
    public void ThereIsNothingToMakeDueWithoutAnOpenBatch()
    {
        Assert.False(new PlayerScoreBatchAccumulator().MakeDue(MixEnum.Phoenix, UserId, Now.UtcDateTime));
    }
}
