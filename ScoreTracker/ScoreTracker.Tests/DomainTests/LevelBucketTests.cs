using System.Linq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class LevelBucketTests
{
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void FromBlankThrows(string value)
    {
        Assert.Throws<InvalidNameException>(() => LevelBucket.From(value));
    }

    [Fact]
    public void FromTrimsWhitespace()
    {
        var bucket = LevelBucket.From("  S15  ");
        Assert.Equal("S15", (string)bucket);
    }

    [Fact]
    public void EqualityIsCaseInsensitive()
    {
        Assert.Equal(LevelBucket.From("S15"), LevelBucket.From("s15"));
        Assert.True(LevelBucket.From("S15") == LevelBucket.From("s15"));
    }

    [Fact]
    public void DifferentBucketsAreNotEqual()
    {
        Assert.NotEqual(LevelBucket.From("S15"), LevelBucket.From("S16"));
        Assert.True(LevelBucket.From("S15") != LevelBucket.From("S16"));
    }

    [Fact]
    public void GetDifficultiesParsesSinglePrefix()
    {
        var difficulties = LevelBucket.From("S15").GetDifficulties();
        Assert.Single(difficulties);
        Assert.Contains((ChartType.Single, DifficultyLevel.From(15)), difficulties);
    }

    [Fact]
    public void GetDifficultiesParsesDoublePrefix()
    {
        var difficulties = LevelBucket.From("D20").GetDifficulties();
        Assert.Single(difficulties);
        Assert.Contains((ChartType.Double, DifficultyLevel.From(20)), difficulties);
    }

    [Fact]
    public void GetDifficultiesParsesCoOpPrefix()
    {
        var difficulties = LevelBucket.From("C3").GetDifficulties();
        Assert.Single(difficulties);
        Assert.Contains((ChartType.CoOp, DifficultyLevel.From(3)), difficulties);
    }

    [Fact]
    public void GetDifficultiesWithoutPrefixIncludesSingleAndDouble()
    {
        var difficulties = LevelBucket.From("17").GetDifficulties().ToHashSet();
        Assert.Contains((ChartType.Single, DifficultyLevel.From(17)), difficulties);
        Assert.Contains((ChartType.Double, DifficultyLevel.From(17)), difficulties);
    }

    [Fact]
    public void GetDifficultiesParsesCommaSeparatedSections()
    {
        var difficulties = LevelBucket.From("S15,D20").GetDifficulties().ToHashSet();
        Assert.Contains((ChartType.Single, DifficultyLevel.From(15)), difficulties);
        Assert.Contains((ChartType.Double, DifficultyLevel.From(20)), difficulties);
    }

    [Fact]
    public void TryParseReturnsTrueForValid()
    {
        Assert.True(LevelBucket.TryParse("S15", out var result));
        Assert.Equal("S15", (string)result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseReturnsFalseForBlank(string input)
    {
        Assert.False(LevelBucket.TryParse(input, out _));
    }

    [Fact]
    public void ContainsIsCaseInsensitive()
    {
        var bucket = LevelBucket.From("S15");
        Assert.True(bucket.Contains("s"));
        Assert.True(bucket.Contains("15"));
        Assert.False(bucket.Contains("D"));
    }
}
