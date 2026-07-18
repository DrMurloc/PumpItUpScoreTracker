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
    public void TheEstimateCountsCoOpWhereTheOfficialPhoenix2FormulaZeroesIt()
    {
        var estimate = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);
        var official = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, true);
        var perfect = PhoenixScore.From(1_000_000);

        // Flat co-op base 2000 × (PG grade 1.50 + PG plate bonus 0.020).
        Assert.Equal(3040, CoOpBoardCalculator.Rating(estimate, perfect));
        Assert.Equal(0, (int)official.GetScore(ChartType.CoOp, DifficultyLevel.From(10), perfect,
            PhoenixPlate.PerfectGame));
    }

    [Fact]
    public void TheGradeAndPlateStepTogetherAcrossTheInferenceBoundary()
    {
        var estimate = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);

        // 995,000 is both the SSS+ grade line and the inferred-UG line: 2000 × (1.50 + 0.016).
        Assert.Equal(3032, CoOpBoardCalculator.Rating(estimate, PhoenixScore.From(995_000)));
        // One point under sits on SSS with an inferred SG: 2000 × (1.49 + 0.008).
        Assert.Equal(2996, CoOpBoardCalculator.Rating(estimate, PhoenixScore.From(994_999)));
    }

    [Fact]
    public void BuildingTheEstimateNeverLeaksIntoAFreshOfficialConfig()
    {
        _ = CoOpBoardCalculator.EstimateScoring(MixEnum.Phoenix2);

        Assert.Equal(0.0,
            ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, true)
                .ChartTypeModifiers[ChartType.CoOp]);
    }
}
