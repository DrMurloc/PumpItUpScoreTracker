using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixTitleProgressTests
{
    private static PhoenixDifficultyTitle DifficultyTitle(int level = 20, int requiredRating = 1000) =>
        new(Name.From($"Lv.{level}"), DifficultyLevel.From(level), requiredRating);

    private static RecordedPhoenixScore Attempt(PhoenixScore? score, bool isBroken = false) =>
        new(Guid.NewGuid(), score, PhoenixPlate.PerfectGame, isBroken, DateTimeOffset.UtcNow);

    [Fact]
    public void RequiredAaCountForDifficultyTitleIsCeilingOfRequiredRatingOverBaseRating()
    {
        var level = DifficultyLevel.From(20);
        // Pick a required rating that doesn't divide cleanly so the ceiling is exercised.
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating * 3 + 1);

        var progress = new PhoenixTitleProgress(title);

        Assert.Equal(4, progress.RequiredAaCount);
    }

    [Fact]
    public void RequiredAaCountIsZeroForNonDifficultyTitles()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome to Phoenix");

        var progress = new PhoenixTitleProgress(title);

        Assert.Equal(0, progress.RequiredAaCount);
    }

    [Fact]
    public void ApplyAttemptAccumulatesCompletionCountFromTitleProgress()
    {
        var progress = new PhoenixTitleProgress(DifficultyTitle());
        var chart = new ChartBuilder().WithLevel(20).Build();
        var rating = DifficultyLevel.From(20).BaseRating;

        progress.ApplyAttempt(chart, Attempt(950000)); // AAA bracket
        var afterFirst = progress.CompletionCount;
        progress.ApplyAttempt(chart, Attempt(950000));

        Assert.Equal(rating * PhoenixLetterGrade.AAA.GetModifier(), afterFirst);
        Assert.Equal(afterFirst * 2, progress.CompletionCount);
    }

    [Fact]
    public void ApplyAttemptDoesNotIncrementWhenAttemptScoreIsNull()
    {
        var progress = new PhoenixTitleProgress(DifficultyTitle());
        var chart = new ChartBuilder().WithLevel(20).Build();

        progress.ApplyAttempt(chart, Attempt(null));

        Assert.Equal(0, progress.CompletionCount);
        Assert.Equal(ParagonLevel.None, progress.ParagonLevel);
    }

    [Fact]
    public void ParagonLevelIsNoneForNonDifficultyTitlesEvenAfterAttempts()
    {
        var basicTitle = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome");
        var progress = new PhoenixTitleProgress(basicTitle);
        var chart = new ChartBuilder().WithLevel(20).Build();

        progress.ApplyAttempt(chart, Attempt(990000));

        Assert.Equal(ParagonLevel.None, progress.ParagonLevel);
    }

    [Fact]
    public void ParagonLevelClimbsAsAaPlusAttemptsAccumulate()
    {
        // RequiredRating = BaseRating * 1 → RequiredAaCount = 1, so a single AA attempt
        // is enough to reach the AA paragon level.
        var level = DifficultyLevel.From(20);
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating);
        var progress = new PhoenixTitleProgress(title);
        var chart = new ChartBuilder().WithLevel(20).Build();

        Assert.Equal(ParagonLevel.None, progress.ParagonLevel);

        progress.ApplyAttempt(chart, Attempt(900000)); // AA bracket → bumps F..AA all to 1

        Assert.Equal(ParagonLevel.AA, progress.ParagonLevel);
    }

    [Fact]
    public void NextParagonProgressReportsBucketAtNextHigherParagonLevel()
    {
        var level = DifficultyLevel.From(20);
        // RequiredAaCount = 2 keeps us at AA after two AA attempts; next level (AA+) sits at 0.
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating * 2);
        var progress = new PhoenixTitleProgress(title);
        var chart = new ChartBuilder().WithLevel(20).Build();

        progress.ApplyAttempt(chart, Attempt(900000)); // AA
        progress.ApplyAttempt(chart, Attempt(900000)); // AA
        // Now F..AA each have count 2, AA+ has 0.

        Assert.Equal(ParagonLevel.AA, progress.ParagonLevel);
        Assert.Equal(0, progress.NextParagonProgress);
    }

    [Fact]
    public void NextParagonProgressIsNegativeOneAtParagonGrade()
    {
        var level = DifficultyLevel.From(20);
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating);
        var progress = new PhoenixTitleProgress(title);
        var chart = new ChartBuilder().WithLevel(20).Build();

        progress.ApplyAttempt(chart, Attempt(1000000)); // PG promotes every paragon bucket including PG

        Assert.Equal(ParagonLevel.PG, progress.ParagonLevel);
        Assert.Equal(-1, progress.NextParagonProgress);
    }

    [Fact]
    public void AdditionalNoteIsEmptyForNonDifficultyAndNonCoOpTitles()
    {
        var basicTitle = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome");
        var progress = new PhoenixTitleProgress(basicTitle);

        Assert.Equal(string.Empty, progress.AdditionalNote);
    }

    [Fact]
    public void AdditionalNoteIsEmptyOnceCompletionCountReachesRequired()
    {
        var level = DifficultyLevel.From(20);
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating);
        var progress = new PhoenixTitleProgress(title);
        var chart = new ChartBuilder().WithLevel(20).Build();

        // PG attempt awards BaseRating * 1.6 (PgLetterGradeModifier handled in helper). For
        // any score that fully covers RequiredRating, CompletionCount >= CompletionRequired.
        progress.ApplyAttempt(chart, Attempt(990000));
        progress.ApplyAttempt(chart, Attempt(990000));

        Assert.True(progress.CompletionCount >= title.CompletionRequired);
        Assert.Equal(string.Empty, progress.AdditionalNote);
    }

    [Fact]
    public void AdditionalNoteReportsPassRangeForIncompleteDifficultyTitle()
    {
        var level = DifficultyLevel.From(20);
        var title = new PhoenixDifficultyTitle(Name.From("Lv.20"), level, level.BaseRating * 4);
        var progress = new PhoenixTitleProgress(title);

        Assert.Contains("Passes", progress.AdditionalNote);
    }

    [Fact]
    public void AdditionalNoteReportsPassRangeForCoOpTitle()
    {
        var coOpTitle = new PhoenixCoOpTitle(Name.From("CoOp Hero"), 4000);
        var progress = new PhoenixTitleProgress(coOpTitle);

        Assert.Contains("Passes", progress.AdditionalNote);
    }
}
