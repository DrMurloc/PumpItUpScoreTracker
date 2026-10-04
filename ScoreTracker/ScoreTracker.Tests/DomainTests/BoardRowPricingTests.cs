using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What one mirrored chart-board row is worth in PUMBILITY. A row knows its chart's type and
///     level and its score, and nothing about the plate.
/// </summary>
public sealed class BoardRowPricingTests
{
    [Fact]
    public void Phoenix2PricesASingleOneLevelUpTheCurveAtTheInferredPlate()
    {
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        // 990,000 is an SSS (1.49) whose likeliest plate is a Marvelous Game (+0.006). Base(24) is
        // 250 for a Double and 260 for a Single.
        Assert.Equal(260 * 1.496, BoardRowPricing.Price(scoring, ChartType.Single, 24, 990_000), 6);
        Assert.Equal(250 * 1.496, BoardRowPricing.Price(scoring, ChartType.Double, 24, 990_000), 6);
    }

    [Fact]
    public void PhoenixPricesBothTypesAlike()
    {
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);

        Assert.Equal(BoardRowPricing.Price(scoring, ChartType.Single, 24, 990_000),
            BoardRowPricing.Price(scoring, ChartType.Double, 24, 990_000), 6);
    }

    [Theory]
    [InlineData("Single", ChartType.Single)]
    [InlineData("Double", ChartType.Double)]
    [InlineData("CoOp", ChartType.CoOp)]
    public void ATypeReadsTheSweepsSpelling(string written, ChartType expected)
    {
        Assert.Equal(expected, BoardRowPricing.TypeOf(written));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Triple")]
    [InlineData("7")]
    public void ARowThatNamesNoTypeHasNone(string? written)
    {
        Assert.Null(BoardRowPricing.TypeOf(written));
    }
}
