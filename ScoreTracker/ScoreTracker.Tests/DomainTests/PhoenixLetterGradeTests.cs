using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Pins the mix-aware score→grade resolution. Phoenix 2 rebalanced the sub-AAA cutoffs upward
///     (verified from live piugame.com boards 2026-07-18); Phoenix keeps the original table. The
///     A-tier boundaries below are the crawl-verified ones — a score sitting on a moved boundary
///     must grade one rung lower under Phoenix 2 than under Phoenix.
/// </summary>
public sealed class PhoenixLetterGradeTests
{
    [Theory]
    // Scores in the rebalanced band grade one rung lower on Phoenix 2 (P1 grade, P2 grade).
    [InlineData(899999, PhoenixLetterGrade.APlus, PhoenixLetterGrade.A)]
    [InlineData(900000, PhoenixLetterGrade.AA, PhoenixLetterGrade.APlus)]
    [InlineData(919999, PhoenixLetterGrade.AA, PhoenixLetterGrade.APlus)]
    [InlineData(925000, PhoenixLetterGrade.AAPlus, PhoenixLetterGrade.AA)]
    [InlineData(939999, PhoenixLetterGrade.AAPlus, PhoenixLetterGrade.AA)]
    public void ATierBoundariesGradeOneRungLowerOnPhoenix2(int score, PhoenixLetterGrade phoenix,
        PhoenixLetterGrade phoenix2)
    {
        Assert.Equal(phoenix, PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix));
        Assert.Equal(phoenix2, PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix2));
    }

    [Theory]
    // AAA and up are identical across mixes — the rebalance stopped at 950k.
    [InlineData(950000, PhoenixLetterGrade.AAA)]
    [InlineData(960000, PhoenixLetterGrade.AAAPlus)]
    [InlineData(970000, PhoenixLetterGrade.S)]
    [InlineData(990000, PhoenixLetterGrade.SSS)]
    [InlineData(1000000, PhoenixLetterGrade.SSSPlus)]
    public void AaaAndAboveAreIdenticalAcrossMixes(int score, PhoenixLetterGrade expected)
    {
        Assert.Equal(expected, PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix));
        Assert.Equal(expected, PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix2));
    }

    [Fact]
    public void TheSubAaaRecutSplitsNineHundredThousandByMix()
    {
        // 900k is AA on the Phoenix table and A+ on Phoenix 2 — there is no mix-less
        // resolution anymore, so every caller states which table it means.
        Assert.Equal(PhoenixLetterGrade.AA, PhoenixScore.From(900000).LetterGradeFor(MixEnum.Phoenix));
        Assert.Equal(PhoenixLetterGrade.APlus, PhoenixScore.From(900000).LetterGradeFor(MixEnum.Phoenix2));
    }

    [Fact]
    public void Phoenix2FloorForAGradeResolvesBackToThatGrade()
    {
        foreach (var grade in System.Enum.GetValues<PhoenixLetterGrade>())
            Assert.Equal(grade, grade.GetMinimumScoreFor(MixEnum.Phoenix2).LetterGradeFor(MixEnum.Phoenix2));
    }
}
