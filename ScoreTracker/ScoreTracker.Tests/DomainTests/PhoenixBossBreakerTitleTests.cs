using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixBossBreakerTitleTests
{
    private const string Song = "Conflict";

    private static PhoenixBossBreakerTitle Title(string songName = Song,
        ChartType type = ChartType.Single, int level = 26) =>
        new(Name.From("Phoenix"), Name.From(songName), type, DifficultyLevel.From(level));

    private static RecordedPhoenixScore Attempt(bool isBroken, PhoenixScore? score = null) =>
        new(Guid.NewGuid(), score, PhoenixPlate.PerfectGame, isBroken, DateTimeOffset.UtcNow);

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
    public void CompletionProgressIsOneWhenAttemptIsClearedOnTheBossChart()
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(1, title.CompletionProgress(chart, Attempt(isBroken: false)));
    }

    [Fact]
    public void CompletionProgressIsZeroWhenAttemptIsBrokenEvenOnMatchingChart()
    {
        var title = Title();
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(0, title.CompletionProgress(chart, Attempt(isBroken: true)));
    }

    [Fact]
    public void CompletionProgressIsZeroForNonMatchingChart()
    {
        var title = Title();
        var otherChart = new ChartBuilder().WithSongName("Other").WithType(ChartType.Single).WithLevel(26).Build();

        Assert.Equal(0, title.CompletionProgress(otherChart, Attempt(isBroken: false)));
    }

    [Fact]
    public void PopulatesFromDatabaseIsFalse()
    {
        Assert.False(Title().PopulatesFromDatabase);
    }

    [Fact]
    public void RequiresExactlyOneCompletion()
    {
        Assert.Equal(1, Title().CompletionRequired);
    }
}
