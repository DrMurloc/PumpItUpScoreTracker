using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CoOpBoardCalculatorTests
{
    [Theory]
    [InlineData(812_000, PhoenixPlate.SuperbGame)]
    [InlineData(994_999, PhoenixPlate.SuperbGame)]
    [InlineData(995_000, PhoenixPlate.UltimateGame)]
    [InlineData(999_999, PhoenixPlate.UltimateGame)]
    [InlineData(1_000_000, PhoenixPlate.PerfectGame)]
    public void PlatesInferFromScoreAlone(int score, PhoenixPlate expected)
    {
        Assert.Equal(expected, CoOpBoardCalculator.InferredPlate(PhoenixScore.From(score)));
    }

    [Fact]
    public void TheBoardPricesAtTheMixesRealCoOpRatingScale()
    {
        var estimate = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);
        var perfect = PhoenixScore.From(1_000_000);

        // Phoenix 2's flat co-op base 80 × (PG grade 1.50 + PG plate bonus 0.020) — the same
        // number the account's own CO-OP Rating pays for that chart, so the board reads on the
        // scale of the [CO-OP] title ladder (LV.1 at 1,000, MASTER at 16,000).
        Assert.Equal(121.60, CoOpBoardCalculator.Rating(estimate, perfect), 2);
        Assert.Equal(CoOpBoardCalculator.Rating(estimate, perfect),
            ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, true)
                .GetScore(ChartType.CoOp, DifficultyLevel.From(2), perfect, PhoenixPlate.PerfectGame));
    }

    [Fact]
    public void TheGradeAndPlateStepTogetherAcrossTheInferenceBoundary()
    {
        var estimate = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);

        // 995,000 is both the SSS+ grade line and the inferred-UG line: 80 × (1.50 + 0.016).
        Assert.Equal(121.28, CoOpBoardCalculator.Rating(estimate, PhoenixScore.From(995_000)), 2);
        // One point under sits on SSS with an inferred SG: 80 × (1.49 + 0.008).
        Assert.Equal(119.84, CoOpBoardCalculator.Rating(estimate, PhoenixScore.From(994_999)), 2);
    }

    [Fact]
    public void ThePhoenixBoardKeepsItsTwoThousandPerChartScale()
    {
        // Phoenix's CO-OP Rating is 2000 × grade, plate-blind: a perfect co-op is 3,000.
        var estimate = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix);
        Assert.Equal(3000, CoOpBoardCalculator.Rating(estimate, PhoenixScore.From(1_000_000)), 2);
    }

    [Fact]
    public void BuildingTheEstimateNeverLeaksIntoAFreshPoolConfig()
    {
        _ = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);

        Assert.Equal(0.0,
            ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
                .ChartTypeModifiers[ChartType.CoOp]);
    }
}
