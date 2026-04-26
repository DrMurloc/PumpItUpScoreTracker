using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixSkillTitleTests
{
    private const string Song = "Conflict";

    private static PhoenixSkillTitle Title(string songName = Song,
        ChartType type = ChartType.Single, int level = 26) =>
        new(Name.From("Speed"), 1, Name.From(songName), type, DifficultyLevel.From(level));

    private static RecordedPhoenixScore Attempt(PhoenixScore? score) =>
        new(Guid.NewGuid(), score, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow);

    [Fact]
    public void AppliesToChartReturnsTrueWhenSongTypeAndLevelMatch()
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();

        Assert.True(title.AppliesToChart(chart));
    }

    [Theory]
    [InlineData("Other Song", ChartType.Single, 26)]
    [InlineData(Song, ChartType.Double, 26)]
    [InlineData(Song, ChartType.Single, 25)]
    public void AppliesToChartReturnsFalseWhenAnyAttributeDiffers(string songName, ChartType type, int level)
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(songName).WithType(type).WithLevel(level).Build();

        Assert.False(title.AppliesToChart(chart));
    }

    [Fact]
    public void CompletionProgressReturnsScoreValueWhenChartMatchesAndScoreRecorded()
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(995000, title.CompletionProgress(chart, Attempt(995000)));
    }

    [Fact]
    public void CompletionProgressReturnsZeroWhenScoreIsNullEvenOnMatchingChart()
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(0, title.CompletionProgress(chart, Attempt(null)));
    }

    [Fact]
    public void CompletionProgressReturnsZeroForNonMatchingChart()
    {
        var title = Title();
        var otherChart = new ChartBuilder().WithSongName("Other").WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(0, title.CompletionProgress(otherChart, Attempt(995000)));
    }

    [Fact]
    public void PopulatesFromDatabaseIsFalse()
    {
        Assert.False(Title().PopulatesFromDatabase);
    }

    [Fact]
    public void RequiresSssScoreToComplete()
    {
        Assert.Equal(990000, Title().CompletionRequired);
    }
}
