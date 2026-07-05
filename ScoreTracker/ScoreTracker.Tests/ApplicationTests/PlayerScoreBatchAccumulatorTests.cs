using System;
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

        Assert.Equal(first, second);
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

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void GapElapsedMintsANewSession()
    {
        var batcher = new PlayerScoreBatchAccumulator();

        var first = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var second = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource,
            Now + TimeSpan.FromHours(9));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SourcesAndMixesTrackSeparateSessions()
    {
        var batcher = new PlayerScoreBatchAccumulator();

        var manual = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.ManualSource, Now);
        var import = batcher.GetOrExtendSession(MixEnum.Phoenix, UserId, ScoreJournalEntry.OfficialImportSource,
            Now);
        var phoenix2 = batcher.GetOrExtendSession(MixEnum.Phoenix2, UserId, ScoreJournalEntry.ManualSource, Now);

        Assert.NotEqual(manual, import);
        Assert.NotEqual(manual, phoenix2);
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

        Assert.Equal(runId, explicitId);
        Assert.Equal(runId, implicitFollowUp);
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
}
