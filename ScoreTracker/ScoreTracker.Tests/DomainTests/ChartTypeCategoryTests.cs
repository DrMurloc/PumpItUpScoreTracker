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

    [Theory]
    [InlineData(ChartTypeCategory.Single, "S")]
    [InlineData(ChartTypeCategory.Double, "D")]
    [InlineData(ChartTypeCategory.CoOp, "CoOp")]
    public void AFolderWearsItsTypesShorthand(ChartTypeCategory category, string expected)
    {
        Assert.Equal(expected, category.GetShortHand());
    }
}
