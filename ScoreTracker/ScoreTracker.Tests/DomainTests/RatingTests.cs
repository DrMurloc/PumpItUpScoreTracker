using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RatingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void FromNonNegativeSucceeds(int value)
    {
        var rating = Rating.From(value);
        Assert.Equal(value, (int)rating);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void FromNegativeThrows(int value)
    {
        Assert.Throws<InvalidScoreException>(() => Rating.From(value));
    }

    [Fact]
    public void IsValidIsTrueForZero()
    {
        Assert.True(Rating.IsValid(0));
    }

    [Fact]
    public void IsValidIsFalseForNegative()
    {
        Assert.False(Rating.IsValid(-1));
    }

    [Theory]
    [InlineData(20, 10, true)]
    [InlineData(10, 20, false)]
    [InlineData(10, 10, false)]
    public void GreaterThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, Rating.From(left) > Rating.From(right));
    }

    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(20, 10, false)]
    [InlineData(10, 10, false)]
    public void LessThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, Rating.From(left) < Rating.From(right));
    }

    [Fact]
    public void EqualRatingsAreEqual()
    {
        Assert.True(Rating.From(50) == Rating.From(50));
        Assert.False(Rating.From(50) != Rating.From(50));
    }

    [Fact]
    public void CompareToReturnsDifference()
    {
        Assert.Equal(0, Rating.From(50).CompareTo(Rating.From(50)));
        Assert.True(Rating.From(50).CompareTo(Rating.From(40)) > 0);
        Assert.True(Rating.From(50).CompareTo(Rating.From(60)) < 0);
    }

    [Theory]
    [InlineData("100", true)]
    [InlineData("0", true)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void TryParseFromStringHonorsValidity(string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, Rating.TryParse(input, out Rating _));
    }

    [Fact]
    public void NullableTryParseReturnsNullForBlankString()
    {
        Assert.True(Rating.TryParse("   ", out Rating? result));
        Assert.Null(result);
    }

    [Fact]
    public void NullableTryParseReturnsValueForValidNumber()
    {
        Assert.True(Rating.TryParse("100", out Rating? result));
        Assert.NotNull(result);
        Assert.Equal(100, (int)result!.Value);
    }
}
