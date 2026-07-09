using System;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.ValueTypes;
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

    [Fact]
    public void CompletionFloorIsAtNineHundredThousand()
    {
        Assert.Equal(900_000, Title().CompletionFloor);
    }

    [Fact]
    public void PercentCompleteRebasesFromTheFloorNotZero()
    {
        // 945k on a skill chart is halfway from the 900k floor to the 990k target — not ~95%.
        var progress = new PhoenixTitleProgress(Title());
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();
        progress.ApplyAttempt(chart, Attempt(945000));

        Assert.Equal(0.5, progress.PercentComplete, 3);
    }

    [Fact]
    public void PercentCompleteClampsToZeroBelowTheFloor()
    {
        var progress = new PhoenixTitleProgress(Title());
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(26).Build();
        progress.ApplyAttempt(chart, Attempt(880000));

        Assert.Equal(0, progress.PercentComplete);
    }
}
