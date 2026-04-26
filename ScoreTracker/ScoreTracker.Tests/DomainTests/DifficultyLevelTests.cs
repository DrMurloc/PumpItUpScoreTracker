using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class DifficultyLevelTests
{
    [Fact]
    public void DifficultyLowerThan1ThrowsException()
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.From(0));
    }

    [Fact]
    public void DifficultyGreaterThan29ThrowsException()
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.From(30));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(14)]
    public void DifficultyBetween1And29IsFine(int level)
    {
        var difficultyLevel = DifficultyLevel.From(level);
        Assert.Equal(level, (int)difficultyLevel);
    }

    // ---- CompareTo(object) ----

    [Fact]
    public void CompareToObjectWithDifficultyLevelDelegatesToTypedCompareTo()
    {
        DifficultyLevel a = DifficultyLevel.From(15);
        object b = DifficultyLevel.From(20);

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(((DifficultyLevel)20).CompareTo((object)DifficultyLevel.From(20)) == 0);
    }

    [Fact]
    public void CompareToObjectWithIntDelegatesToIntCompareTo()
    {
        DifficultyLevel a = DifficultyLevel.From(15);

        Assert.True(a.CompareTo((object)20) < 0);
        Assert.True(a.CompareTo((object)15) == 0);
        Assert.True(a.CompareTo((object)10) > 0);
    }

    [Fact]
    public void CompareToObjectReturnsZeroForUnrelatedTypes()
    {
        DifficultyLevel a = DifficultyLevel.From(15);

        Assert.Equal(0, a.CompareTo((object)"15"));
        Assert.Equal(0, a.CompareTo((object?)null));
    }

    // ---- ParseShortHand / TryParseShortHand / ToShorthand ----

    [Theory]
    [InlineData("S20", ChartType.Single, 20)]
    [InlineData("D15", ChartType.Double, 15)]
    [InlineData("CoOp3", ChartType.CoOp, 3)]
    [InlineData("  S20  ", ChartType.Single, 20)] // regex tolerates whitespace
    public void ParseShortHandReturnsChartTypeAndLevel(string input, ChartType expectedType, int expectedLevel)
    {
        var (type, level) = DifficultyLevel.ParseShortHand(input);

        Assert.Equal(expectedType, type);
        Assert.Equal(expectedLevel, (int)level);
    }

    [Theory]
    [InlineData("not-a-shorthand")] // fails the regex entirely
    [InlineData("12")]               // matches no letter group
    [InlineData("")]                 // empty input
    public void ParseShortHandThrowsForMalformedInput(string input)
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.ParseShortHand(input));
    }

    [Fact]
    public void ParseShortHandThrowsWhenLevelIsOutOfRange()
    {
        // 30 is past Max; the regex matches but TryParse fails.
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.ParseShortHand("S30"));
    }

    [Fact]
    public void TryParseShortHandReturnsTrueAndOutValuesOnSuccess()
    {
        var ok = DifficultyLevel.TryParseShortHand("D22", out var type, out var level);

        Assert.True(ok);
        Assert.Equal(ChartType.Double, type);
        Assert.Equal(22, (int)level);
    }

    [Fact]
    public void TryParseShortHandReturnsFalseAndDefaultsOnFailure()
    {
        var ok = DifficultyLevel.TryParseShortHand("garbage", out var type, out var level);

        Assert.False(ok);
        Assert.Equal(ChartType.Single, type);
        Assert.Equal(1, (int)level);
    }

    [Theory]
    [InlineData(ChartType.Single, 20, "S20")]
    [InlineData(ChartType.Double, 15, "D15")]
    [InlineData(ChartType.CoOp, 3, "CoOp3")]
    public void ToShorthandFormatsChartTypeAndLevel(ChartType type, int level, string expected)
    {
        Assert.Equal(expected, DifficultyLevel.ToShorthand(type, DifficultyLevel.From(level)));
    }

    [Fact]
    public void ToShorthandRoundTripsThroughParseShortHand()
    {
        var encoded = DifficultyLevel.ToShorthand(ChartType.Double, DifficultyLevel.From(22));
        var (type, level) = DifficultyLevel.ParseShortHand(encoded);

        Assert.Equal(ChartType.Double, type);
        Assert.Equal(22, (int)level);
    }
}