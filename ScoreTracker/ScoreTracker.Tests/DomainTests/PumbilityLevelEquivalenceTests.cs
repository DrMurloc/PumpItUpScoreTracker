using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The level-equivalent axis behind the PUMBILITY calculator's ruler and comparison
///     (docs/design/pumbility-calculator.md D4–D6): a value read as which level's 900,000 is
///     worth the same. The goldens are the page's headline numbers — a change here is a change
///     to what the page tells players, and should be one on purpose.
/// </summary>
public sealed class PumbilityLevelEquivalenceTests
{
    private static ScoringConfiguration Phoenix => ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
    private static ScoringConfiguration Phoenix2 => ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

    public static IEnumerable<object[]> BothMixesBothTypes()
    {
        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        foreach (var type in new[] { ChartType.Single, ChartType.Double })
            yield return new object[] { mix, type };
    }

    [Fact]
    public void TheAnchorIsTheGradeWhoseFloorIs900kOnEachMix()
    {
        // Resolved from the floors, never stated: 900,000 is where AA starts on Phoenix and where A+
        // starts on Phoenix 2 (the sub-AAA floors moved), so the same play anchors both rulers.
        Assert.Equal(PhoenixLetterGrade.AA, PumbilityLevelEquivalence.AnchorGrade(MixEnum.Phoenix));
        Assert.Equal(PhoenixLetterGrade.APlus, PumbilityLevelEquivalence.AnchorGrade(MixEnum.Phoenix2));
        Assert.Equal(900_000, (int)PhoenixLetterGrade.AA.GetMinimumScoreFor(MixEnum.Phoenix));
        Assert.Equal(900_000, (int)PhoenixLetterGrade.APlus.GetMinimumScoreFor(MixEnum.Phoenix2));
    }

    [Theory]
    [MemberData(nameof(BothMixesBothTypes))]
    public void TheAnchorGradeSitsExactlyOnItsOwnLevelEverywhere(MixEnum mix, ChartType type)
    {
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        var anchor = PumbilityLevelEquivalence.AnchorGrade(mix);
        for (var level = 10; level <= 29; level++)
        {
            var value = PumbilityLevelEquivalence.ValueAt(config, type, level, anchor);
            Assert.Equal(level, PumbilityLevelEquivalence.EquivalentLevel(config, type, value), 9);
            Assert.Equal(0, PumbilityLevelEquivalence.LevelsBought(config, type, level, anchor), 9);
        }
    }

    [Fact]
    public void ASingleIsPricedALevelUpAndStillReadsAsItsOwnLevel()
    {
        // The identity above already covers it; this pins the reason it is not trivial — an S17's
        // anchor value is Base(18)'s, so an inversion on the doubles curve would call it an 18.
        var s17 = PumbilityLevelEquivalence.ValueAt(Phoenix2, ChartType.Single, 17, PhoenixLetterGrade.APlus);
        var d18 = PumbilityLevelEquivalence.ValueAt(Phoenix2, ChartType.Double, 18, PhoenixLetterGrade.APlus);
        Assert.Equal(220 * 1.33, s17, 9);
        Assert.Equal(220 * 1.35, d18, 9);
        Assert.Equal(17, PumbilityLevelEquivalence.EquivalentLevel(Phoenix2, ChartType.Single, s17), 9);
    }

    [Theory]
    [MemberData(nameof(BothMixesBothTypes))]
    public void HigherGradesReadAsHigherLevels(MixEnum mix, ChartType type)
    {
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        var grades = Enum.GetValues<PhoenixLetterGrade>();
        var previous = double.NegativeInfinity;
        foreach (var grade in grades)
        {
            var read = PumbilityLevelEquivalence.EquivalentLevel(config, type,
                PumbilityLevelEquivalence.ValueAt(config, type, 20, grade));
            Assert.True(read >= previous, $"{grade} read {read} after {previous}");
            previous = read;
        }
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, ChartType.Single, 16, 2.1)]
    [InlineData(MixEnum.Phoenix, ChartType.Single, 20, 2.7)]
    [InlineData(MixEnum.Phoenix, ChartType.Single, 24, 3.5)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double, 16, 4.7)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double, 20, 4.6)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double, 24, 2.8)]
    [InlineData(MixEnum.Phoenix2, ChartType.Single, 16, 5.5)]
    [InlineData(MixEnum.Phoenix2, ChartType.Single, 20, 4.5)]
    [InlineData(MixEnum.Phoenix2, ChartType.Single, 24, 3.3)]
    public void AnSssPlusBuysThisManyLevelsOfPassPushing(MixEnum mix, ChartType type, int level, double expected)
    {
        // The page's headline exchange rates. Phoenix grows with level (quadratic base); Phoenix 2
        // buys more below the kink at 24 and less above it, where the base steps 10 a level.
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        var bought = PumbilityLevelEquivalence.LevelsBought(config, type, level, PhoenixLetterGrade.SSSPlus);
        Assert.InRange(bought, expected - 0.05, expected + 0.05);
    }

    [Fact]
    public void ScoringBuysMoreLevelsOnPhoenix2ThanPhoenixBelowTheKink()
    {
        for (var level = 16; level <= 21; level++)
        {
            var p1 = PumbilityLevelEquivalence.LevelsBought(Phoenix, ChartType.Single, level, PhoenixLetterGrade.SSSPlus);
            var p2 = PumbilityLevelEquivalence.LevelsBought(Phoenix2, ChartType.Double, level, PhoenixLetterGrade.SSSPlus);
            Assert.True(p2 > p1, $"at {level}: Phoenix 2 {p2:0.00} vs Phoenix {p1:0.00}");
        }

        // And less above it — the comparison section's whole point is that it depends where you are.
        Assert.True(
            PumbilityLevelEquivalence.LevelsBought(Phoenix2, ChartType.Double, 25, PhoenixLetterGrade.SSSPlus) <
            PumbilityLevelEquivalence.LevelsBought(Phoenix, ChartType.Single, 25, PhoenixLetterGrade.SSSPlus));
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, ChartType.Single, PhoenixLetterGrade.S)]
    [InlineData(MixEnum.Phoenix2, ChartType.Single, PhoenixLetterGrade.AA)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double, PhoenixLetterGrade.AAPlus)]
    public void PassingOneLevelHigherIsMatchedByThisGradeAtTwenty(MixEnum mix, ChartType type,
        PhoenixLetterGrade expected)
    {
        // "Passing one level higher took a 900,000 → S on Phoenix; on Phoenix 2 it takes only AA
        // (singles) / AA+ (doubles)" — the sentence the comparison leads with.
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        Assert.Equal(expected, PumbilityLevelEquivalence.GradeMatchingNextLevel(config, type, 20));
    }

    [Fact]
    public void TheTopOfTheLadderHasNoNextLevelToMatch()
    {
        Assert.Null(PumbilityLevelEquivalence.GradeMatchingNextLevel(Phoenix, ChartType.Single, DifficultyLevel.Max));
    }

    [Fact]
    public void ValuesPastEitherEndExtrapolateInsteadOfClamping()
    {
        // An SSS+ on a Phoenix 2 D29 is worth more than any anchor play in the game; the ruler
        // draws it past 29 rather than pinning it to the last chart that exists.
        var top = PumbilityLevelEquivalence.EquivalentLevel(Phoenix2, ChartType.Double,
            PumbilityLevelEquivalence.ValueAt(Phoenix2, ChartType.Double, 29, PhoenixLetterGrade.SSSPlus));
        Assert.True(top > 29, $"read {top}");
        Assert.True(double.IsFinite(top));

        // And a D on a level 10 reads as a level below ten — the curve is not clamped at its foot.
        var bottom = PumbilityLevelEquivalence.EquivalentLevel(Phoenix2, ChartType.Double,
            PumbilityLevelEquivalence.ValueAt(Phoenix2, ChartType.Double, 10, PhoenixLetterGrade.D));
        Assert.True(bottom < 10, $"read {bottom}");
        Assert.True(double.IsFinite(bottom));
    }

    [Fact]
    public void TheValueTableReadsTheLowestPlate()
    {
        // The table prints base × grade at Rough Game; a plate would put a Phoenix 2 bonus into
        // every cell and shift the whole ruler by it.
        var d24s = PumbilityLevelEquivalence.ValueAt(Phoenix2, ChartType.Double, 24, PhoenixLetterGrade.S);
        Assert.Equal(250 * 1.45, d24s, 9);
        var p1 = PumbilityLevelEquivalence.ValueAt(Phoenix, ChartType.Double, 22, PhoenixLetterGrade.SSPlus);
        Assert.Equal(880 * 1.38, p1, 9);
    }
}
