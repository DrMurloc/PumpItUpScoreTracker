using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Where a Phoenix 2 PUMBILITY title sits on the folder ladder. The read is deliberately
///     impersonal, so every assertion here is a fact about the title rather than about a player.
/// </summary>
public sealed class SuggestedTitleLevelTests
{
    private static Title Titled(string name)
    {
        return Phoenix2TitleList.BuildList().Single(t => t.Name == (Name)name);
    }

    private static string Folder(string title)
    {
        var suggestion = SuggestedTitleLevel.For(Titled(title));
        Assert.NotNull(suggestion);
        return string.Join(" ", suggestion!.Folders);
    }

    [Fact]
    public void OnlyPumbilityTitlesHaveAFolderToSuggest()
    {
        // A skill title already names its chart, and a boss breaker is a single clear.
        Assert.Null(SuggestedTitleLevel.For(Titled("[BRACKET] LV.1")));
        Assert.Null(SuggestedTitleLevel.For(Titled("BEGINNER")));
        Assert.Null(SuggestedTitleLevel.For(PhoenixTitleList.GetTitleByName("The Master")));
    }

    [Fact]
    public void ASinglesTitleSuggestsASinglesFolderAndADoublesTitleADoublesOne()
    {
        Assert.StartsWith("S", Folder("[S] ADVANCED LV.5"));
        Assert.StartsWith("D", Folder("[D] ADVANCED LV.5"));
    }

    [Fact]
    public void AMergedPoolTitleNamesBothTypesBecauseEitherCanFillIt()
    {
        var suggestion = SuggestedTitleLevel.For(Titled("[P.B] GOLD"));
        Assert.NotNull(suggestion);
        Assert.Equal(2, suggestion!.Folders.Count);
        Assert.StartsWith("S", suggestion.Folders[0]);
        Assert.StartsWith("D", suggestion.Folders[1]);
    }

    [Fact]
    public void SinglesSuggestALowerFolderThanDoublesForTheSameThreshold()
    {
        // Singles price one level up the base curve, so an S19 is worth what a D20 is.
        var singles = int.Parse(Folder("[S] ADVANCED LV.5")[1..]);
        var doubles = int.Parse(Folder("[D] ADVANCED LV.5")[1..]);
        Assert.Equal(doubles - 1, singles);
    }

    [Fact]
    public void AHarderTitleNeverSuggestsAnEasierFolder()
    {
        var ladder = new[]
        {
            "[S] INTERMEDIATE LV.1", "[S] INTERMEDIATE LV.10", "[S] ADVANCED LV.5",
            "[S] EXPERT LV.1", "[S] EXPERT LV.10", "SINGLE MASTER"
        };
        var levels = ladder.Select(t => int.Parse(Folder(t)[1..])).ToArray();
        Assert.Equal(levels.OrderBy(l => l), levels);
    }

    [Fact]
    public void TheEasiestTitlesBottomOutAtTenRatherThanBelowIt()
    {
        // Phoenix 2 prices a sub-10 chart at zero, so no folder under 10 ever serves.
        Assert.Equal("S10", Folder("[S] INTERMEDIATE LV.1"));
        Assert.Equal("D10", Folder("[D] INTERMEDIATE LV.1"));
    }

    [Fact]
    public void TheAnswerNamesTheReferenceItAssumed()
    {
        var suggestion = SuggestedTitleLevel.For(Titled("[S] ADVANCED LV.5"));
        Assert.NotNull(suggestion);
        Assert.Equal(PhoenixLetterGrade.AAA, suggestion!.Grade);
        Assert.Equal(PhoenixPlate.TalentedGame, suggestion.Plate);
    }

    [Fact]
    public void EveryPumbilityTitleGetsAnAnswer()
    {
        // The top rung asks 20,000 against a 29s ceiling — if the curve ever stops reaching it
        // the drawer would silently drop the line rather than say so.
        var pumbility = Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>().ToArray();
        Assert.Equal(70, pumbility.Length);
        Assert.All(pumbility, t => Assert.NotNull(SuggestedTitleLevel.For(t)));
    }
}
