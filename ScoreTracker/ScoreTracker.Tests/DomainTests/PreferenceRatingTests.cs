using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PreferenceRatingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(5)]
    public void FromInRangeSucceeds(decimal value)
    {
        var rating = PreferenceRating.From(value);
        Assert.Equal(value, (decimal)rating);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(5.01)]
    [InlineData(100)]
    public void FromOutOfRangeThrows(decimal value)
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => PreferenceRating.From(value));
    }

    [Fact]
    public void ImplicitFromIntCallsFrom()
    {
        PreferenceRating r = 3;
        Assert.Equal(3m, (decimal)r);
    }

    [Fact]
    public void ImplicitToIntTruncates()
    {
        var r = PreferenceRating.From(3.7m);
        int v = r;
        Assert.Equal(3, v);
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, false)]
    public void GreaterThanComparesUnderlyingValue(decimal left, decimal right, bool expected)
    {
        Assert.Equal(expected, PreferenceRating.From(left) > PreferenceRating.From(right));
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 2, false)]
    public void LessOrEqualComparesUnderlyingValue(decimal left, decimal right, bool expected)
    {
        Assert.Equal(expected, PreferenceRating.From(left) <= PreferenceRating.From(right));
    }

    [Fact]
    public void EqualRatingsAreEqual()
    {
        Assert.True(PreferenceRating.From(2.5m) == PreferenceRating.From(2.5m));
    }

    [Fact]
    public void CompareToReturnsDifferenceSign()
    {
        Assert.Equal(0, PreferenceRating.From(2m).CompareTo(PreferenceRating.From(2m)));
        Assert.True(PreferenceRating.From(3m).CompareTo(PreferenceRating.From(2m)) > 0);
        Assert.True(PreferenceRating.From(1m).CompareTo(PreferenceRating.From(2m)) < 0);
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("3.5", true)]
    [InlineData("0", true)]
    [InlineData("5", true)]
    [InlineData("6", false)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void TryParseHonorsValidity(string input, bool expected)
    {
        Assert.Equal(expected, PreferenceRating.TryParse(input, out _));
    }
}
