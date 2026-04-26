using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class BpmTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromNonPositiveThrows(decimal value)
    {
        Assert.Throws<InvalidBpmException>(() => Bpm.From(value));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(0.5)]
    [InlineData(999.999)]
    public void FromPositiveProducesEqualMinAndMax(decimal value)
    {
        var bpm = Bpm.From(value);
        Assert.Equal(value, bpm.Min);
        Assert.Equal(value, bpm.Max);
        Assert.Equal(value, bpm.Average);
    }

    [Fact]
    public void FromRangeAcceptsMinLessThanMax()
    {
        var bpm = Bpm.From(120m, 200m);
        Assert.Equal(120m, bpm.Min);
        Assert.Equal(200m, bpm.Max);
        Assert.Equal(160m, bpm.Average);
    }

    [Fact]
    public void FromRangeAcceptsEqualMinAndMax()
    {
        var bpm = Bpm.From(120m, 120m);
        Assert.Equal(120m, bpm.Min);
        Assert.Equal(120m, bpm.Max);
    }

    [Fact]
    public void FromRangeWithMaxLessThanMinThrows()
    {
        Assert.Throws<InvalidBpmException>(() => Bpm.From(200m, 120m));
    }

    [Fact]
    public void FromRangeWithNonPositiveMinThrows()
    {
        Assert.Throws<InvalidBpmException>(() => Bpm.From(0m, 120m));
    }

    [Fact]
    public void FromRangeWithNonPositiveMaxThrows()
    {
        Assert.Throws<InvalidBpmException>(() => Bpm.From(-1m, 0m));
    }

    [Fact]
    public void NullableFromReturnsNullWhenBothNull()
    {
        Assert.Null(Bpm.From(null, null));
    }

    [Fact]
    public void NullableFromUsesMinWhenMaxIsNull()
    {
        var bpm = Bpm.From(120m, null);
        Assert.NotNull(bpm);
        Assert.Equal(120m, bpm!.Value.Min);
        Assert.Equal(120m, bpm.Value.Max);
    }

    [Fact]
    public void NullableFromUsesMaxWhenMinIsNull()
    {
        var bpm = Bpm.From(null, 200m);
        Assert.NotNull(bpm);
        Assert.Equal(200m, bpm!.Value.Min);
        Assert.Equal(200m, bpm.Value.Max);
    }

    [Fact]
    public void EqualBpmsAreEqual()
    {
        Assert.True(Bpm.From(120m) == Bpm.From(120m));
        Assert.False(Bpm.From(120m) != Bpm.From(120m));
    }

    [Fact]
    public void DifferentBpmsAreNotEqual()
    {
        Assert.False(Bpm.From(120m) == Bpm.From(140m));
        Assert.True(Bpm.From(120m) != Bpm.From(140m));
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData("99.5", 99.5)]
    public void TryParseReadsSingleValue(string input, decimal expected)
    {
        Assert.True(Bpm.TryParse(input, out var result));
        Assert.Equal(expected, result.Min);
        Assert.Equal(expected, result.Max);
    }

    [Fact]
    public void TryParseReadsRange()
    {
        Assert.True(Bpm.TryParse("120 ~ 200", out var result));
        Assert.Equal(120m, result.Min);
        Assert.Equal(200m, result.Max);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("120 - 200")]
    [InlineData("")]
    public void TryParseRejectsInvalidInput(string input)
    {
        Assert.False(Bpm.TryParse(input, out _));
    }

    // ---- Implicit conversions ----

    [Fact]
    public void ImplicitConversionFromDecimalDelegatesToFrom()
    {
        Bpm bpm = 120m;
        Assert.Equal(120m, bpm.Min);
        Assert.Equal(120m, bpm.Max);
    }

    [Fact]
    public void ImplicitConversionFromNonPositiveDecimalThrows()
    {
        Assert.Throws<InvalidBpmException>(() =>
        {
            Bpm bpm = 0m;
        });
    }

    [Theory]
    [InlineData("120", 120, 120)]
    [InlineData("99.5", 99.5, 99.5)]
    [InlineData("120 ~ 200", 120, 200)]
    public void ImplicitConversionFromStringDelegatesToTryParse(string input, decimal expectedMin, decimal expectedMax)
    {
        Bpm bpm = input;
        Assert.Equal(expectedMin, bpm.Min);
        Assert.Equal(expectedMax, bpm.Max);
    }

    [Fact]
    public void ImplicitConversionFromMalformedStringThrows()
    {
        Assert.Throws<InvalidBpmException>(() =>
        {
            Bpm bpm = "not-a-number";
        });
    }
}
