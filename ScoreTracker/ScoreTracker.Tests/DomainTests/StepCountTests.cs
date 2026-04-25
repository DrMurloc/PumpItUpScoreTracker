using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class StepCountTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5000)]
    public void FromNonNegativeIntSucceeds(int value)
    {
        var count = StepCount.From(value);
        Assert.Equal(value, (int)count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void FromNegativeIntThrows(int value)
    {
        Assert.Throws<InvalidStepCountException>(() => StepCount.From(value));
    }

    [Fact]
    public void ImplicitFromIntCallsFrom()
    {
        StepCount count = 42;
        Assert.Equal(42, (int)count);
    }

    [Fact]
    public void ImplicitToIntReturnsUnderlying()
    {
        var count = StepCount.From(42);
        int value = count;
        Assert.Equal(42, value);
    }

    [Fact]
    public void EqualCountsAreEqual()
    {
        Assert.True(StepCount.From(5) == StepCount.From(5));
        Assert.False(StepCount.From(5) != StepCount.From(5));
    }

    [Fact]
    public void DifferentCountsAreNotEqual()
    {
        Assert.False(StepCount.From(5) == StepCount.From(6));
        Assert.True(StepCount.From(5) != StepCount.From(6));
    }

    [Theory]
    [InlineData("100", true)]
    [InlineData("0", true)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void TryParseFromStringHonorsValidity(string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, StepCount.TryParse(input, out _));
    }

    [Fact]
    public void TryParseFromIntReturnsTrueForValid()
    {
        Assert.True(StepCount.TryParse(100, out var result));
        Assert.Equal(100, (int)result);
    }

    [Fact]
    public void TryParseFromIntReturnsFalseForNegative()
    {
        Assert.False(StepCount.TryParse(-1, out _));
    }
}
