using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class XXSkillTitleTests
{
    private const string Song = "Conflict";

    private static XXSkillTitle Title(XXLetterGrade required = XXLetterGrade.SS) =>
        new(Name.From("Speed Hero"), Name.From(Song), ChartType.Single, DifficultyLevel.From(20), required);

    private static BestXXChartAttempt Attempt(string song = Song, ChartType type = ChartType.Single,
        int level = 20, XXLetterGrade letter = XXLetterGrade.SS, bool isBroken = false)
    {
        var chart = new ChartBuilder().WithSongName(song).WithType(type).WithLevel(level).Build();
        var bestAttempt = new XXChartAttempt(letter, isBroken, null, DateTimeOffset.UtcNow);
        return new BestXXChartAttempt(chart, bestAttempt);
    }

    [Fact]
    public void DefaultLetterGradeRequirementIsSS()
    {
        // The two-arg constructor delegates to SS as the default required grade.
        var defaultTitle = new XXSkillTitle(Name.From("Default"), Name.From(Song), ChartType.Single,
            DifficultyLevel.From(20));

        Assert.True(defaultTitle.DoesAttemptApply(Attempt(letter: XXLetterGrade.SS)));
        Assert.False(defaultTitle.DoesAttemptApply(Attempt(letter: XXLetterGrade.S)));
    }

    [Fact]
    public void DoesAttemptApplyReturnsFalseWhenBestAttemptIsNull()
    {
        var chart = new ChartBuilder().WithSongName(Song).WithType(ChartType.Single).WithLevel(20).Build();
        var attempt = new BestXXChartAttempt(chart, null);

        Assert.False(Title().DoesAttemptApply(attempt));
    }

    [Theory]
    [InlineData("Other Song", ChartType.Single, 20, XXLetterGrade.SS)]
    [InlineData(Song, ChartType.Double, 20, XXLetterGrade.SS)]
    [InlineData(Song, ChartType.Single, 19, XXLetterGrade.SS)]
    public void DoesAttemptApplyReturnsFalseWhenChartAttributesDiffer(string song, ChartType type, int level,
        XXLetterGrade letter)
    {
        Assert.False(Title().DoesAttemptApply(Attempt(song, type, level, letter)));
    }

    [Fact]
    public void DoesAttemptApplyReturnsFalseWhenLetterGradeBelowRequirement()
    {
        Assert.False(Title(required: XXLetterGrade.SS).DoesAttemptApply(Attempt(letter: XXLetterGrade.S)));
    }

    [Fact]
    public void DoesAttemptApplyReturnsTrueWhenLetterGradeMeetsRequirement()
    {
        Assert.True(Title(required: XXLetterGrade.SS).DoesAttemptApply(Attempt(letter: XXLetterGrade.SS)));
    }

    [Fact]
    public void DoesAttemptApplyReturnsTrueWhenLetterGradeExceedsRequirement()
    {
        Assert.True(Title(required: XXLetterGrade.SS).DoesAttemptApply(Attempt(letter: XXLetterGrade.SSS)));
    }

    [Fact]
    public void DoesAttemptApplyDoesNotConsiderBrokenFlag()
    {
        // The skill title only checks song/type/level/letter — broken state is unrelated.
        Assert.True(Title().DoesAttemptApply(Attempt(letter: XXLetterGrade.SS, isBroken: true)));
    }
}
