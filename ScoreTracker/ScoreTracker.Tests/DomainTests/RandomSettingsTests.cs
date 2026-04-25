using System.Linq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RandomSettingsTests
{
    [Fact]
    public void DefaultLevelWeightsCoverAllDifficulties()
    {
        var settings = new RandomSettings();
        foreach (var level in DifficultyLevel.All)
            Assert.Equal(0, settings.LevelWeights[(int)level]);
    }

    [Fact]
    public void DefaultDoubleLevelWeightsCoverAllDifficulties()
    {
        var settings = new RandomSettings();
        foreach (var level in DifficultyLevel.All)
            Assert.Equal(0, settings.DoubleLevelWeights[(int)level]);
    }

    [Fact]
    public void DefaultPlayerCountWeightsCoverTwoThroughFive()
    {
        var settings = new RandomSettings();
        Assert.Equal(new[] { 2, 3, 4, 5 }, settings.PlayerCountWeights.Keys.OrderBy(k => k));
        Assert.All(settings.PlayerCountWeights.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void DefaultHasWeightedSettingIsFalse()
    {
        Assert.False(new RandomSettings().HasWeightedSetting);
    }

    [Fact]
    public void HasWeightedSettingIsTrueWhenLevelWeightExceedsOne()
    {
        var settings = new RandomSettings();
        var firstLevel = DifficultyLevel.All.First();
        settings.LevelWeights[(int)firstLevel] = 2;
        Assert.True(settings.HasWeightedSetting);
    }

    [Fact]
    public void HasWeightedSettingIsFalseWhenAllWeightsAreOneOrLess()
    {
        var settings = new RandomSettings();
        foreach (var level in DifficultyLevel.All)
            settings.LevelWeights[(int)level] = 1;
        Assert.False(settings.HasWeightedSetting);
    }

    [Fact]
    public void HasWeightedSettingIsTrueWhenSongTypeWeightExceedsOne()
    {
        var settings = new RandomSettings();
        settings.SongTypeWeights[SongType.Arcade] = 5;
        Assert.True(settings.HasWeightedSetting);
    }

    [Fact]
    public void ClearLevelWeightsZerosLevelWeightsAndDoublesAndPlayerCounts()
    {
        var settings = new RandomSettings();
        var firstLevel = DifficultyLevel.All.First();
        settings.LevelWeights[(int)firstLevel] = 5;
        settings.DoubleLevelWeights[(int)firstLevel] = 7;
        settings.PlayerCountWeights[2] = 9;

        settings.ClearLevelWeights();

        Assert.All(settings.LevelWeights.Values, v => Assert.Equal(0, v));
        Assert.All(settings.DoubleLevelWeights.Values, v => Assert.Equal(0, v));
        Assert.All(settings.PlayerCountWeights.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void ClearLevelWeightsLeavesSongTypeWeightsAlone()
    {
        var settings = new RandomSettings();
        settings.SongTypeWeights[SongType.Arcade] = 3;

        settings.ClearLevelWeights();

        Assert.Equal(3, settings.SongTypeWeights[SongType.Arcade]);
    }

    [Fact]
    public void ClearChartTypeMinimumsResetsToNullForEachKnownType()
    {
        var settings = new RandomSettings();
        settings.ChartTypeMinimums[ChartType.Single] = 5;
        settings.ChartTypeMinimums[ChartType.Double] = 10;

        settings.ClearChartTypeMinimums();

        Assert.Equal(3, settings.ChartTypeMinimums.Count);
        Assert.All(settings.ChartTypeMinimums.Values, v => Assert.Null(v));
    }

    [Fact]
    public void ClearLevelMinimumsResetsAllDifficultyKeysToNull()
    {
        var settings = new RandomSettings();
        var firstLevel = DifficultyLevel.All.First();
        settings.LevelMinimums[(int)firstLevel] = 1;

        settings.ClearLevelMinimums();

        Assert.All(settings.LevelMinimums.Values, v => Assert.Null(v));
    }

    [Fact]
    public void ClearCustomMinimumsRemovesAllEntries()
    {
        var settings = new RandomSettings();
        settings.CustomMinimums["foo"] = 1;
        settings.CustomMinimums["bar"] = 2;

        settings.ClearCustomMinimums();

        Assert.Empty(settings.CustomMinimums);
    }

    [Fact]
    public void CustomMinimumsKeysAreCaseInsensitive()
    {
        var settings = new RandomSettings();
        settings.CustomMinimums["Foo"] = 1;

        Assert.True(settings.CustomMinimums.ContainsKey("foo"));
        Assert.True(settings.CustomMinimums.ContainsKey("FOO"));
    }

    [Fact]
    public void DefaultOrderingIsRandomized()
    {
        Assert.Equal(RandomSettings.ResultsOrdering.Randomized, new RandomSettings().Ordering);
    }

    [Fact]
    public void DefaultCountIsThree()
    {
        Assert.Equal(3, new RandomSettings().Count);
    }
}
