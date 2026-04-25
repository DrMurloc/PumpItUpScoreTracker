using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class XXScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(995010)]
    [InlineData(200000000)]
    public void FromInRangeSucceeds(int value)
    {
        var score = XXScore.From(value);
        Assert.Equal(value, (int)score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(200000001)]
    public void FromOutOfRangeThrows(int value)
    {
        Assert.Throws<InvalidScoreException>(() => XXScore.From(value));
    }

    [Fact]
    public void ImplicitFromIntCallsFrom()
    {
        XXScore s = 1000;
        Assert.Equal(1000, (int)s);
    }

    [Fact]
    public void ImplicitToIntReturnsUnderlying()
    {
        int v = XXScore.From(1000);
        Assert.Equal(1000, v);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(50, 100, false)]
    [InlineData(50, 50, false)]
    public void GreaterThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, XXScore.From(left) > XXScore.From(right));
    }

    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(100, 50, false)]
    [InlineData(50, 50, false)]
    public void LessThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, XXScore.From(left) < XXScore.From(right));
    }

    [Fact]
    public void EqualScoresAreEqual()
    {
        Assert.True(XXScore.From(1000) == XXScore.From(1000));
        Assert.False(XXScore.From(1000) != XXScore.From(1000));
    }

    [Theory]
    [InlineData("100", true)]
    [InlineData("0", true)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void TryParseFromStringHonorsValidity(string input, bool expected)
    {
        Assert.Equal(expected, XXScore.TryParse(input, out XXScore _));
    }

    [Fact]
    public void NullableTryParseReturnsNullForBlank()
    {
        Assert.True(XXScore.TryParse("   ", out XXScore? result));
        Assert.Null(result);
    }
}
