using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartAttemptTests
{
    [Theory]
    [InlineData(LetterGrade.SSS, LetterGrade.SS)]
    [InlineData(LetterGrade.SS, LetterGrade.S)]
    [InlineData(LetterGrade.S, LetterGrade.A)]
    [InlineData(LetterGrade.A, LetterGrade.B)]
    [InlineData(LetterGrade.B, LetterGrade.C)]
    [InlineData(LetterGrade.C, LetterGrade.D)]
    [InlineData(LetterGrade.D, LetterGrade.F)]
    public void HigherLetterIsGreaterThanLower(LetterGrade letterGradeA, LetterGrade letterGradeB)
    {
        var attemptA = new ChartAttempt(letterGradeA, true);
        var attemptB = new ChartAttempt(letterGradeB, true);
        Assert.True(attemptA > attemptB);
        Assert.True(attemptB < attemptA);
    }

    [InlineData(LetterGrade.SSS, true)]
    [InlineData(LetterGrade.SS, false)]
    [InlineData(LetterGrade.S, true)]
    [InlineData(LetterGrade.A, false)]
    [InlineData(LetterGrade.B, true)]
    [InlineData(LetterGrade.C, false)]
    [InlineData(LetterGrade.D, true)]
    public void SameGradeAndBrokenIsNotGreaterThanEither(LetterGrade letterGrade, bool isBroken)
    {
        var attemptA = new ChartAttempt(letterGrade, isBroken);
        var attemptB = new ChartAttempt(letterGrade, isBroken);
        Assert.False(attemptA > attemptB);
        Assert.False(attemptB < attemptA);
        Assert.False(attemptA < attemptB);
        Assert.False(attemptB > attemptA);
    }

    [Theory]
    [InlineData(LetterGrade.SSS)]
    [InlineData(LetterGrade.SS)]
    [InlineData(LetterGrade.S)]
    [InlineData(LetterGrade.A)]
    [InlineData(LetterGrade.B)]
    [InlineData(LetterGrade.C)]
    [InlineData(LetterGrade.D)]
    public void UnbrokenIsGreaterThanBroken(LetterGrade letterGrade)
    {
        var attemptA = new ChartAttempt(letterGrade, true);
        var attemptB = new ChartAttempt(letterGrade, false);

        Assert.True(attemptA > attemptB);
        Assert.True(attemptB < attemptA);
    }
}