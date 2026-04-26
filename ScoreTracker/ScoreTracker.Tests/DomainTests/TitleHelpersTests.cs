using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TitleHelpersTests
{
    private static PhoenixTitleProgress DifficultyProgress(int level, int requiredRating, bool complete)
    {
        var title = new PhoenixDifficultyTitle(Name.From($"Lv.{level}"), DifficultyLevel.From(level), requiredRating);
        var progress = new PhoenixTitleProgress(title);
        if (complete)
        {
            // GetPushingTitle compares CompletionCount to CompletionRequired, not IsComplete,
            // so push enough rating in to actually meet the bar.
            var chart = new ChartBuilder().WithLevel(level).Build();
            var attempt = new RecordedPhoenixScore(Guid.NewGuid(), 1000000, PhoenixPlate.PerfectGame, false,
                DateTimeOffset.UtcNow);
            while (progress.CompletionCount < title.CompletionRequired)
                progress.ApplyAttempt(chart, attempt);
        }
        return progress;
    }

    [Fact]
    public void GetPushingTitleReturnsLowestDifficultyTitleWhenNoneAreComplete()
    {
        var lvl15 = DifficultyProgress(15, 1000, complete: false);
        var lvl20 = DifficultyProgress(20, 5000, complete: false);
        var lvl25 = DifficultyProgress(25, 10000, complete: false);

        var pushing = new TitleProgress[] { lvl25, lvl15, lvl20 }.GetPushingTitle();

        Assert.Same(lvl15, pushing);
    }

    [Fact]
    public void GetPushingTitleReturnsTitleAboveTheHighestCompletedDifficultyTitle()
    {
        var lvl15 = DifficultyProgress(15, 1000, complete: true);
        var lvl20 = DifficultyProgress(20, 5000, complete: true);
        var lvl25 = DifficultyProgress(25, 10000, complete: false);

        var pushing = new TitleProgress[] { lvl15, lvl25, lvl20 }.GetPushingTitle();

        Assert.Same(lvl25, pushing);
    }

    [Fact]
    public void GetPushingTitleIgnoresNonDifficultyTitles()
    {
        var lvl15 = DifficultyProgress(15, 1000, complete: true);
        var lvl20 = DifficultyProgress(20, 5000, complete: false);
        var basicTitle = new PhoenixTitleProgress(new PhoenixBasicTitle(Name.From("Welcome"), "Welcome"));

        var pushing = new TitleProgress[] { basicTitle, lvl15, lvl20 }.GetPushingTitle();

        Assert.Same(lvl20, pushing);
    }

    [Fact]
    public void GetPushingTitleSortsBySameLevelByCompletionRequired()
    {
        // Two level-20 difficulty titles with different completion requirements; the easier
        // one is incomplete and should be the pushing target.
        var easyAtLvl20 = DifficultyProgress(20, 1000, complete: false);
        var hardAtLvl20 = DifficultyProgress(20, 5000, complete: false);

        var pushing = new TitleProgress[] { hardAtLvl20, easyAtLvl20 }.GetPushingTitle();

        Assert.Same(easyAtLvl20, pushing);
    }
}
