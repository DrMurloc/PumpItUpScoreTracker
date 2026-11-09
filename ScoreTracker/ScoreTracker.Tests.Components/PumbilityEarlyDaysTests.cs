using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The tier list's early-days line (docs/design/rise.md D33): the community PUMBILITY list says
///     how few full top 50s it was read off, below the cutoff the PUMBILITY calculator already uses.
/// </summary>
public sealed class PumbilityEarlyDaysTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(99)]
    public void ACommunityPumbilityListOfFewPoolsSaysSo(int pools)
    {
        Assert.True(PumbilityEarlyDays.Applies(false, "PUMBILITY", pools));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(2_400)]
    public void APopulationAtTheCutoffOrAboveSaysNothing(int pools)
    {
        Assert.False(PumbilityEarlyDays.Applies(false, "PUMBILITY", pools));
    }

    [Fact]
    public void NoPoolsAtAllIsAnEmptyListNotAThinOne()
    {
        Assert.False(PumbilityEarlyDays.Applies(false, "PUMBILITY", 0));
    }

    [Theory]
    [InlineData(true, "PUMBILITY")]
    [InlineData(false, "Pass")]
    [InlineData(false, "Score")]
    public void OnlyTheCommunityPumbilityListCountsPools(bool personalized, string lens)
    {
        Assert.False(PumbilityEarlyDays.Applies(personalized, lens, 8));
    }
}
