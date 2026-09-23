using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartTypeCategoryTests
{
    [Theory]
    [InlineData(ChartType.Single, ChartTypeCategory.Single)]
    [InlineData(ChartType.SinglePerformance, ChartTypeCategory.Single)]
    [InlineData(ChartType.Double, ChartTypeCategory.Double)]
    [InlineData(ChartType.DoublePerformance, ChartTypeCategory.Double)]
    [InlineData(ChartType.HalfDouble, ChartTypeCategory.Double)]
    [InlineData(ChartType.CoOp, ChartTypeCategory.CoOp)]
    public void EveryTypeCountsInOneFolder(ChartType type, ChartTypeCategory expected)
    {
        Assert.Equal(expected, type.Category());
        Assert.True(expected.Includes(type));
    }

    [Fact]
    public void EveryTypeIsListedUnderTheFolderItCountsIn()
    {
        foreach (var type in Enum.GetValues<ChartType>())
            Assert.Contains(type, type.Category().TypesIn());

        Assert.Equal(Enum.GetValues<ChartType>().Length,
            Enum.GetValues<ChartTypeCategory>().Sum(c => c.TypesIn().Count));
    }

    /// <summary>
    ///     The names are the contract: tier-list routes, the TierLists__ChartType UiSetting and
    ///     API parameters already carry these exact strings, so a category has to round-trip
    ///     through every value written before it existed.
    /// </summary>
    [Theory]
    [InlineData("Single", ChartTypeCategory.Single)]
    [InlineData("Double", ChartTypeCategory.Double)]
    [InlineData("CoOp", ChartTypeCategory.CoOp)]
    public void StoredChartTypeStringsStillParse(string stored, ChartTypeCategory expected)
    {
        Assert.True(Enum.TryParse<ChartTypeCategory>(stored, true, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(stored, expected.ToString());
    }

    /// <summary>
    ///     A contract that still takes one ChartType has to be handed the type the mix actually
    ///     has, or it asks for charts that are not there: RISE has no Double rows at all.
    /// </summary>
    [Theory]
    [InlineData(MixEnum.Rise, ChartType.HalfDouble)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double)]
    [InlineData(MixEnum.RiseArcade, ChartType.Double)]
    [InlineData(MixEnum.Infinity, ChartType.Double)]
    public void AFolderAnswersTheTypeTheMixHasForIt(MixEnum mix, ChartType expected)
    {
        Assert.Equal(expected, ChartTypeCategory.Double.TypeOn(mix));
        Assert.Equal(ChartType.Single, ChartTypeCategory.Single.TypeOn(mix));
    }

    // A mix without the folder at all still answers something rather than throwing.
    [Fact]
    public void AFolderTheMixDoesNotHaveFallsBackToItsHeadType()
    {
        Assert.Equal(ChartType.CoOp, ChartTypeCategory.CoOp.TypeOn(MixEnum.Rise));
    }

    /// <summary>
    ///     Enum.TryParse accepts any number, so "/TierLists/7/20" would parse to an undefined
    ///     category and throw the moment anything asked it for its type or its shorthand. A route
    ///     segment and a query parameter are reader-supplied data, not a contract.
    /// </summary>
    [Theory]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("HalfDouble")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUndefinedFolderDoesNotParse(string? text)
    {
        Assert.False(ChartTypeCategories.TryParse(text, out _));
    }

    [Theory]
    [InlineData("Double", ChartTypeCategory.Double)]
    [InlineData("coop", ChartTypeCategory.CoOp)]
    public void ADefinedFolderStillParsesCaseInsensitively(string text, ChartTypeCategory expected)
    {
        Assert.True(ChartTypeCategories.TryParse(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(ChartTypeCategory.Single, "S")]
    [InlineData(ChartTypeCategory.Double, "D")]
    [InlineData(ChartTypeCategory.CoOp, "CoOp")]
    public void AFolderWearsItsTypesShorthand(ChartTypeCategory category, string expected)
    {
        Assert.Equal(expected, category.GetShortHand());
    }
}
