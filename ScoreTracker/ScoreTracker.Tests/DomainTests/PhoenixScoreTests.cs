using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(995010)]
    [InlineData(1000000)]
    public void FromInRangeSucceeds(int value)
    {
        var score = PhoenixScore.From(value);
        Assert.Equal(value, (int)score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void FromOutOfRangeThrows(int value)
    {
        Assert.Throws<InvalidScoreException>(() => PhoenixScore.From(value));
    }

    [Fact]
    public void IsValidIsTrueForBoundaryValues()
    {
        Assert.True(PhoenixScore.IsValid(0));
        Assert.True(PhoenixScore.IsValid(1000000));
    }

    [Fact]
    public void IsValidIsFalseOutsideBoundaries()
    {
        Assert.False(PhoenixScore.IsValid(-1));
        Assert.False(PhoenixScore.IsValid(1000001));
    }

    [Theory]
    [InlineData(995010, 4, 1000000)]
    [InlineData(995010, 3, 995000)]
    [InlineData(994499, 3, 994000)]
    [InlineData(0, 4, 0)]
    public void RoundUsesPowerOfTen(int score, int zeros, int expected)
    {
        var rounded = PhoenixScore.From(score).Round(zeros);
        Assert.Equal(expected, (int)rounded);
    }

    [Theory]
    [InlineData(995000, 990000, true)]
    [InlineData(990000, 995000, false)]
    [InlineData(995000, 995000, false)]
    public void GreaterThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, PhoenixScore.From(left) > PhoenixScore.From(right));
    }

    [Theory]
    [InlineData(990000, 995000, true)]
    [InlineData(995000, 990000, false)]
    [InlineData(995000, 995000, false)]
    public void LessThanComparesUnderlyingScore(int left, int right, bool expected)
    {
        Assert.Equal(expected, PhoenixScore.From(left) < PhoenixScore.From(right));
    }

    [Fact]
    public void EqualScoresAreEqual()
    {
        Assert.True(PhoenixScore.From(995010) == PhoenixScore.From(995010));
        Assert.False(PhoenixScore.From(995010) != PhoenixScore.From(995010));
    }

    [Fact]
    public void DifferentScoresAreNotEqual()
    {
        Assert.False(PhoenixScore.From(995010) == PhoenixScore.From(995011));
        Assert.True(PhoenixScore.From(995010) != PhoenixScore.From(995011));
    }

    [Fact]
    public void CompareToOrdersByUnderlyingScore()
    {
        Assert.Equal(0, PhoenixScore.From(995010).CompareTo(PhoenixScore.From(995010)));
        Assert.True(PhoenixScore.From(1000000).CompareTo(PhoenixScore.From(0)) > 0);
        Assert.True(PhoenixScore.From(0).CompareTo(PhoenixScore.From(1000000)) < 0);
    }

    [Fact]
    public void CompareToWithIntComparesUnderlyingValue()
    {
        Assert.Equal(0, PhoenixScore.From(995010).CompareTo(995010));
        Assert.True(PhoenixScore.From(995010).CompareTo(995009) > 0);
    }

    [Fact]
    public void ImplicitFromIntCallsFrom()
    {
        PhoenixScore s = 995010;
        Assert.Equal(995010, (int)s);
    }

    [Theory]
    [InlineData("995010", true)]
    [InlineData("0", true)]
    [InlineData("1000001", false)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void TryParseFromStringHonorsValidity(string input, bool expected)
    {
        Assert.Equal(expected, PhoenixScore.TryParse(input, out PhoenixScore _));
    }

    [Fact]
    public void NullableTryParseReturnsNullForBlank()
    {
        Assert.True(PhoenixScore.TryParse("   ", out PhoenixScore? result));
        Assert.Null(result);
    }

    [Fact]
    public void ToStringContainsAllDigits()
    {
        var rendered = PhoenixScore.From(995010).ToString();
        Assert.Contains("995", rendered);
        Assert.Contains("010", rendered);
    }
}
