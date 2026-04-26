using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixDifficultyTitleTests
{
    private static PhoenixDifficultyTitle Title(int level = 20, int requiredRating = 5000) =>
        new(Name.From($"Lv.{level}"), DifficultyLevel.From(level), requiredRating);

    private static RecordedPhoenixScore Attempt(PhoenixScore? score, bool isBroken = false) =>
        new(Guid.NewGuid(), score, PhoenixPlate.PerfectGame, isBroken, DateTimeOffset.UtcNow);

    [Fact]
    public void ExposesLevelAndRequiredRating()
    {
        var title = Title(level: 22, requiredRating: 7500);

        Assert.Equal(DifficultyLevel.From(22), title.Level);
        Assert.Equal(7500, title.RequiredRating);
        Assert.Equal(7500, title.CompletionRequired);
    }

    [Fact]
    public void CompletionProgressReturnsZeroWhenChartLevelDiffers()
    {
        var title = Title(level: 20);
        var offLevel = new ChartBuilder().WithLevel(19).Build();

        Assert.Equal(0, title.CompletionProgress(offLevel, Attempt(990000)));
    }

    [Fact]
    public void CompletionProgressReturnsZeroWhenAttemptIsBroken()
    {
        var title = Title(level: 20);
        var chart = new ChartBuilder().WithLevel(20).Build();

        Assert.Equal(0, title.CompletionProgress(chart, Attempt(990000, isBroken: true)));
    }

    [Fact]
    public void CompletionProgressReturnsZeroWhenScoreIsNull()
    {
        var title = Title(level: 20);
        var chart = new ChartBuilder().WithLevel(20).Build();

        Assert.Equal(0, title.CompletionProgress(chart, Attempt(null)));
    }

    [Fact]
    public void CompletionProgressIsBaseRatingTimesLetterGradeModifier()
    {
        var level = DifficultyLevel.From(20);
        var title = Title(level: 20);
        var chart = new ChartBuilder().WithLevel(20).Build();

        // 950000 falls in the AAA bracket; modifier is the static value the enum reports.
        var attempt = Attempt(950000);
        var expected = level.BaseRating * PhoenixLetterGrade.AAA.GetModifier();

        Assert.Equal(expected, title.CompletionProgress(chart, attempt));
    }

    [Fact]
    public void PopulatesFromDatabaseIsFalse()
    {
        Assert.False(Title().PopulatesFromDatabase);
    }

    [Fact]
    public void CategoryIsDifficulty()
    {
        Assert.Equal("Difficulty", (string)Title().Category);
    }
}
