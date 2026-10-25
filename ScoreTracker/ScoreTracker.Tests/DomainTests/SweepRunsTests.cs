using System;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Which failed run is finished next: the earliest complete download nobody is still working on,
///     at least a week's spacing after the latest finished week.
/// </summary>
public sealed class SweepRunsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastSunday = new(2026, 10, 11, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sunday = LastSunday.AddDays(7);
    private static readonly DateTimeOffset Tuesday = Sunday.AddDays(2);
    private static readonly DateTimeOffset NextSunday = Sunday.AddDays(7);

    private static SnapshotRun Failed(int id, DateTimeOffset startedAt, string stage = SweepRuns.HighlightsStage,
        string? error = "Execution Timeout Expired", DateTimeOffset? lastProgressAt = null) =>
        new(id, startedAt, null, false, stage, 0, 0, 0, error, lastProgressAt ?? startedAt.AddMinutes(57));

    private static SnapshotRun Finished(int id, DateTimeOffset startedAt) =>
        new(id, startedAt, startedAt.AddMinutes(55), false, "Sealed", 0, 0, 0, null, startedAt.AddMinutes(52));

    [Fact]
    public void ACompleteDownloadAWeekAfterTheLatestFinishedWeekIsNext()
    {
        var next = SweepRuns.NextToFinish(new[] { Failed(27, Sunday) }, Finished(26, LastSunday), Now);

        Assert.Equal(27, next?.Id);
    }

    [Fact]
    public void AMixWithNoFinishedWeekFinishesItsFirst()
    {
        Assert.Equal(27, SweepRuns.NextToFinish(new[] { Failed(27, Sunday) }, null, Now)?.Id);
    }

    [Fact]
    public void ARunThatDiedBeforeItsDownloadFinishedIsNotFinished()
    {
        var next = SweepRuns.NextToFinish(new[] { Failed(27, Sunday, "ChartBoards") }, Finished(26, LastSunday),
            Now);

        Assert.Null(next);
    }

    [Fact]
    public void AFinishedRunIsNotFinishedAgain()
    {
        Assert.Null(SweepRuns.NextToFinish(new[] { Finished(27, Sunday) }, Finished(26, LastSunday), Now));
    }

    [Fact]
    public void TheSundayGoesBeforeItsReTrigger()
    {
        // Finishing Tuesday's re-trigger first would make it the week and strand Sunday behind it.
        var next = SweepRuns.NextToFinish(new[] { Failed(28, Tuesday), Failed(27, Sunday) },
            Finished(26, LastSunday), Now);

        Assert.Equal(27, next?.Id);
    }

    [Fact]
    public void OnceSundayIsFinishedTheReTriggerIsTooSoonAndTheNextSundayIsNext()
    {
        var next = SweepRuns.NextToFinish(new[] { Failed(28, Tuesday), Failed(29, NextSunday) },
            Finished(27, Sunday), Now);

        Assert.Equal(29, next?.Id);
    }

    [Fact]
    public void ARunStillWorkingOnItsWeekIsNotFinished()
    {
        var working = Failed(30, Now.AddHours(-1), error: null, lastProgressAt: Now.AddMinutes(-3));

        Assert.Null(SweepRuns.NextToFinish(new[] { working }, Finished(29, Now.AddDays(-7)), Now));
    }

    [Fact]
    public void ARunWhoseProcessDiedIsFinishedOnceItStopsBeating()
    {
        // A deploy kills the process mid-week: no error is ever written, the heartbeat just stops.
        var killed = Failed(30, Now.AddHours(-2), error: null, lastProgressAt: Now.AddMinutes(-90));

        Assert.Equal(30, SweepRuns.NextToFinish(new[] { killed }, Finished(29, Now.AddDays(-7)), Now)?.Id);
    }
}
