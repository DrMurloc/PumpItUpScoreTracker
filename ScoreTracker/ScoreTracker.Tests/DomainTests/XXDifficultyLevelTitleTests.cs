using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class XXDifficultyLevelTitleTests
{
    private static BestXXChartAttempt Attempt(int level, bool isBroken = false)
    {
        var chart = new ChartBuilder().WithLevel(level).Build();
        var bestAttempt = new XXChartAttempt(XXLetterGrade.A, isBroken, null, DateTimeOffset.UtcNow);
        return new BestXXChartAttempt(chart, bestAttempt);
    }

    [Fact]
    public void DoesAttemptApplyReturnsFalseWhenBestAttemptIsNull()
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 100);
        var chart = new ChartBuilder().WithLevel(20).Build();
        var attempt = new BestXXChartAttempt(chart, null);

        Assert.False(title.DoesAttemptApply(attempt));
    }

    [Fact]
    public void DoesAttemptApplyReturnsFalseWhenAttemptIsBroken()
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 100);

        Assert.False(title.DoesAttemptApply(Attempt(level: 20, isBroken: true)));
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(19, false)]
    public void SingleLevelConstructorMatchesOnlyExactLevel(int chartLevel, bool expected)
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 100);

        Assert.Equal(expected, title.DoesAttemptApply(Attempt(level: chartLevel)));
    }

    [Theory]
    [InlineData(18, false)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    [InlineData(22, false)]
    public void RangeConstructorMatchesAnyLevelInsideTheRangeInclusive(int chartLevel, bool expected)
    {
        var title = new XXDifficultyLevelTitle(Name.From("19-21"), DifficultyLevel.From(19),
            DifficultyLevel.From(21), 100);

        Assert.Equal(expected, title.DoesAttemptApply(Attempt(level: chartLevel)));
    }

    [Fact]
    public void AdditionalRequirementsConstructorIncludesNoteInDescription()
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 100,
            "Stage breaks not counted");

        Assert.Contains("Stage breaks not counted", title.Description);
    }

    [Fact]
    public void RequiredCountFlowsThroughToCompletionRequired()
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 250);

        Assert.Equal(250, title.CompletionRequired);
    }

    [Fact]
    public void CategoryIsDifficulty()
    {
        var title = new XXDifficultyLevelTitle(Name.From("Lv.20"), DifficultyLevel.From(20), 100);

        Assert.Equal("Difficulty", (string)title.Category);
    }
}
