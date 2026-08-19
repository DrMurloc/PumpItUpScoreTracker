using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SessionUndoReplayTests
{
    private static readonly Guid Chart = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 2, 18, 0, 0, TimeSpan.Zero);

    private static ScoreJournalEntry Play(int minutesIn, int score, bool isBroken = false,
        string source = ScoreJournalEntry.OfficialImportSource, PhoenixPlate? plate = null)
    {
        return new ScoreJournalEntry(Start.AddMinutes(minutesIn), source, Guid.NewGuid(), Chart,
            (PhoenixScore)score, plate, isBroken, MixEnum.Phoenix);
    }

    private static ScoreJournalEntry StageBreak(int minutesIn)
    {
        return new ScoreJournalEntry(Start.AddMinutes(minutesIn), ScoreJournalEntry.OfficialImportSource,
            Guid.NewGuid(), Chart, null, null, true, MixEnum.Phoenix, IsBest: false, IsStageBroken: true);
    }

    [Fact]
    public void NothingSurvivingMeansTheRecordGoesEntirely()
    {
        // A chart the undone session was the first to touch returns to never having been played.
        Assert.Null(SessionUndoReplay.BestOf(Array.Empty<ScoreJournalEntry>()));
    }

    [Fact]
    public void AStageBreakIsNeverCrownedEvenAsTheOnlySurvivor()
    {
        // The first survivor is taken unconditionally, which is exactly how an undo could seat
        // a stage break the write path refused. It cannot: a stage break is history, not a
        // candidate.
        Assert.Null(SessionUndoReplay.BestOf(new[] { StageBreak(0), StageBreak(5) }));

        var best = SessionUndoReplay.BestOf(new[] { StageBreak(0), Play(10, 900_000, isBroken: true), StageBreak(20) });
        Assert.Equal((PhoenixScore)900_000, best!.Score);
        Assert.False(best.IsStageBroken);
    }

    [Fact]
    public void TheBestSurvivingPlayBecomesTheRecord()
    {
        var best = SessionUndoReplay.BestOf(new[] { Play(0, 900_000), Play(10, 940_000), Play(20, 920_000) });

        Assert.Equal((PhoenixScore)940_000, best!.Score);
    }

    [Fact]
    public void ALaterSessionsPlayKeepsItsScore()
    {
        // The independence guarantee: undoing one session leaves every other alone, including
        // newer ones, because their plays are still there to be replayed.
        var best = SessionUndoReplay.BestOf(new[] { Play(0, 900_000), Play(500, 985_000) });

        Assert.Equal((PhoenixScore)985_000, best!.Score);
    }

    [Fact]
    public void OrderOfArrivalDoesNotChangeTheOutcome()
    {
        var plays = new[] { Play(0, 900_000), Play(10, 940_000), Play(20, 920_000) };

        var forwards = SessionUndoReplay.BestOf(plays);
        var backwards = SessionUndoReplay.BestOf(plays.Reverse());

        Assert.Equal(forwards!.Score, backwards!.Score);
        Assert.Equal(forwards.OccurredAt, backwards.OccurredAt);
    }

    [Fact]
    public void APassOutranksAHigherBreak()
    {
        var best = SessionUndoReplay.BestOf(new[]
        {
            Play(0, 850_000),
            Play(10, 990_000, isBroken: true)
        });

        Assert.False(best!.IsBroken);
        Assert.Equal((PhoenixScore)850_000, best.Score);
    }

    [Fact]
    public void AManualCorrectionDownwardIsNotUndoneByTheReplay()
    {
        // Manual and CSV are authoritative and may lower a record. A replay that only ever took
        // the maximum would quietly resurrect the score the player corrected away from.
        var best = SessionUndoReplay.BestOf(new[]
        {
            Play(0, 980_000),
            Play(10, 700_000, source: ScoreJournalEntry.ManualSource)
        });

        Assert.Equal((PhoenixScore)700_000, best!.Score);
    }

    [Fact]
    public void AnImportCannotLowerARecordTheWayAManualEntryCan()
    {
        var best = SessionUndoReplay.BestOf(new[]
        {
            Play(0, 980_000),
            Play(10, 700_000)
        });

        Assert.Equal((PhoenixScore)980_000, best!.Score);
    }

    [Fact]
    public void ANonBestObservationStillCountsWhenEverythingAboveItIsGone()
    {
        // Since the journal carries plays that never became a record, the replay reads them all
        // — filtering on IsBest first would drop the row about to become the new best.
        var observation = Play(5, 910_000) with { IsBest = false };

        var best = SessionUndoReplay.BestOf(new List<ScoreJournalEntry> { observation });

        Assert.Equal((PhoenixScore)910_000, best!.Score);
    }
}
