using System;
using ScoreTracker.Domain.Models;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     XX and older track score and grade as separate measurements, so a best is a composite
///     that no single run necessarily produced. This is deliberately the opposite of
///     BestAttemptPolicy, which moves Phoenix's axes together to stop a plate dragging a score
///     down — the tests below exist so a future unification attempt fails loudly.
/// </summary>
public sealed class LegacyBestAttemptPolicyTests
{
    private static readonly DateTimeOffset Then = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);

    private static XXChartAttempt Attempt(XXLetterGrade grade, int? score, bool broken = false,
        DateTimeOffset? on = null) =>
        new(grade, broken, score == null ? null : (XXScore?)score.Value, on ?? Then);

    [Fact]
    public void ABetterScoreRaisesTheScoreAndLeavesTheGradeAlone()
    {
        var stored = Attempt(XXLetterGrade.SS, 900_000);

        var merged = LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.A, 950_000), Now);

        Assert.Equal(XXLetterGrade.SS, merged.LetterGrade);
        Assert.Equal(950_000, (int)merged.Score!.Value);
    }

    [Fact]
    public void ABetterGradeRaisesTheGradeAndLeavesTheScoreAlone()
    {
        var stored = Attempt(XXLetterGrade.A, 950_000);

        var merged = LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.SSS, 800_000), Now);

        Assert.Equal(XXLetterGrade.SSS, merged.LetterGrade);
        Assert.Equal(950_000, (int)merged.Score!.Value);
    }

    /// <summary>
    ///     The composite: neither axis came from the same run, which is exactly the shape
    ///     Phoenix forbids and legacy requires.
    /// </summary>
    [Fact]
    public void TheBestCanBeAGradeAndAScoreFromDifferentRuns()
    {
        var first = Attempt(XXLetterGrade.SSS, 700_000);
        var merged = LegacyBestAttemptPolicy.Merge(first, Attempt(XXLetterGrade.B, 990_000), Now);

        Assert.Equal(XXLetterGrade.SSS, merged.LetterGrade);
        Assert.Equal(990_000, (int)merged.Score!.Value);
    }

    /// <summary>
    ///     Most legacy records carry no number at all (4.8% of production rows have one), so a
    ///     grade-only submission has to mean "leave the score alone", never "the score is gone".
    /// </summary>
    [Fact]
    public void AGradeOnlySubmissionNeverClearsAStoredScore()
    {
        var stored = Attempt(XXLetterGrade.A, 900_000);

        var merged = LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.SS, null), Now);

        Assert.Equal(XXLetterGrade.SS, merged.LetterGrade);
        Assert.Equal(900_000, (int)merged.Score!.Value);
    }

    [Fact]
    public void AScoreLandsOnARecordThatNeverHadOne()
    {
        var merged = LegacyBestAttemptPolicy.Merge(Attempt(XXLetterGrade.A, null),
            Attempt(XXLetterGrade.A, 500_000), Now);

        Assert.Equal(500_000, (int)merged.Score!.Value);
    }

    /// <summary>A pass outranks a break whatever the letters — the same call Phoenix makes.</summary>
    [Fact]
    public void APassOutranksABreakEvenWithAWorseLetter()
    {
        var stored = Attempt(XXLetterGrade.SSS, 900_000, broken: true);

        var merged = LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.C, 800_000), Now);

        Assert.Equal(XXLetterGrade.C, merged.LetterGrade);
        Assert.False(merged.IsBroken);
        // The score axis did not improve, so it stays where it was.
        Assert.Equal(900_000, (int)merged.Score!.Value);
    }

    [Fact]
    public void ABrokenRunNeverDisplacesAPassingGrade()
    {
        var stored = Attempt(XXLetterGrade.B, 700_000);

        var merged = LegacyBestAttemptPolicy.Merge(stored,
            Attempt(XXLetterGrade.SSS, 600_000, broken: true), Now);

        Assert.Equal(XXLetterGrade.B, merged.LetterGrade);
        Assert.False(merged.IsBroken);
    }

    [Fact]
    public void ASubmissionThatImprovesNothingIsNotHistory()
    {
        var stored = Attempt(XXLetterGrade.SS, 900_000);

        Assert.False(LegacyBestAttemptPolicy.Beats(stored, Attempt(XXLetterGrade.A, 800_000)));
        Assert.Same(stored, LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.A, 800_000), Now));
    }

    /// <summary>The date marks the last real improvement, so a no-op re-import cannot restamp it.</summary>
    [Fact]
    public void TheDateMovesOnlyWhenSomethingImproved()
    {
        var stored = Attempt(XXLetterGrade.SS, 900_000);

        Assert.Equal(Then, LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.A, 100), Now).RecordedOn);
        Assert.Equal(Now, LegacyBestAttemptPolicy.Merge(stored, Attempt(XXLetterGrade.SSS, 100), Now).RecordedOn);
    }

    [Fact]
    public void TheFirstAttemptIsTheBest()
    {
        var incoming = Attempt(XXLetterGrade.C, 100_000);

        Assert.True(LegacyBestAttemptPolicy.Beats(null, incoming));
        Assert.Same(incoming, LegacyBestAttemptPolicy.Merge(null, incoming, Now));
    }
}
