using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Where a Phoenix 2 PUMBILITY title sits on the folder ladder, at each of three reference
///     grades. The read is deliberately impersonal, so every assertion here is a fact about the
///     title rather than about a player.
/// </summary>
public sealed class SuggestedTitleLevelTests
{
    private static Title Titled(string name)
    {
        return Phoenix2TitleList.BuildList().Single(t => t.Name == (Name)name);
    }

    private static SuggestedLevel Suggestion(string title)
    {
        var suggestion = SuggestedTitleLevel.For(Titled(title));
        Assert.NotNull(suggestion);
        return suggestion!;
    }

    /// <summary>The rung a given grade answers on, whether it stands alone or leads a merged run.</summary>
    private static SuggestedRung RungAt(string title, PhoenixLetterGrade grade)
    {
        var rungs = Suggestion(title).Rungs;
        // Rungs descend by grade and a merged one is named for the lowest grade it covers, so the
        // first rung whose floor is at or below the asked grade is the one that answers for it.
        var rung = rungs.FirstOrDefault(r => r.Grade <= grade);
        Assert.NotNull(rung);
        return rung!;
    }

    private static string Folder(string title, PhoenixLetterGrade grade)
    {
        return string.Join(" ", RungAt(title, grade).Folders);
    }

    /// <summary>The AAA answer, which is the one this used to print on its own.</summary>
    private static string Folder(string title)
    {
        return Folder(title, PhoenixLetterGrade.AAA);
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
        var rung = RungAt("[P.B] GOLD", PhoenixLetterGrade.AAA);
        Assert.Equal(2, rung.Folders.Count);
        Assert.StartsWith("S", rung.Folders[0]);
        Assert.StartsWith("D", rung.Folders[1]);
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
    public void TheAnswerNamesThePlateItAssumed()
    {
        Assert.Equal(PhoenixPlate.TalentedGame, Suggestion("[S] ADVANCED LV.5").Plate);
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

    // ---- The three rungs ----

    [Fact]
    public void TheRungsRunBestGradeFirstSoTheLevelsAscend()
    {
        var rungs = Suggestion("[S] ADVANCED LV.1").Rungs;
        Assert.Equal(
            new[] { PhoenixLetterGrade.SSSPlus, PhoenixLetterGrade.AAA, PhoenixLetterGrade.A },
            rungs.Select(r => r.Grade));

        var levels = rungs.Select(r => int.Parse(r.Folders[0][1..])).ToArray();
        Assert.Equal(levels.OrderBy(l => l), levels);
    }

    [Fact]
    public void PlayingBetterAsksForALowerFolder()
    {
        // The whole point of three rungs: the same title is eight folders apart end to end.
        Assert.Equal("S13", Folder("[S] ADVANCED LV.1", PhoenixLetterGrade.SSSPlus));
        Assert.Equal("S16", Folder("[S] ADVANCED LV.1", PhoenixLetterGrade.AAA));
        Assert.Equal("S20", Folder("[S] ADVANCED LV.1", PhoenixLetterGrade.A));
    }

    [Fact]
    public void TheMiddleRungIsWhatTheDrawerUsedToPrintAlone()
    {
        // AAA on a TG plate was the single fixed reference before this became three rungs.
        Assert.Equal("D17", Folder("[D] ADVANCED LV.1", PhoenixLetterGrade.AAA));
        Assert.Equal("S16 D17", Folder("[P.B] GOLD", PhoenixLetterGrade.AAA));
    }

    [Fact]
    public void GradesThatLandOnTheSameFolderMergeIntoOneRung()
    {
        // The level-10 floor puts SSS+ and AAA both on S10 here; two identical rows would read
        // as a rendering fault.
        var rungs = Suggestion("[S] INTERMEDIATE LV.9").Rungs;
        Assert.Equal(2, rungs.Count);

        var merged = rungs[0];
        Assert.Equal("S10", Assert.Single(merged.Folders));
        Assert.Equal(PhoenixLetterGrade.AAA, merged.Grade);
        Assert.True(merged.OrBetter);

        Assert.Equal("S14", Assert.Single(rungs[1].Folders));
        Assert.False(rungs[1].OrBetter);
    }

    [Fact]
    public void ATitleTheFloorFlattensCollapsesToASingleRung()
    {
        var rungs = Suggestion("[S] INTERMEDIATE LV.1").Rungs;
        var only = Assert.Single(rungs);
        Assert.Equal("S10", Assert.Single(only.Folders));
        // Named for the lowest grade in the run, so the drawer can say "at A or better".
        Assert.Equal(PhoenixLetterGrade.A, only.Grade);
        Assert.True(only.OrBetter);
    }

    [Fact]
    public void AGradeNoFolderReachesNamesTheCeilingItFallsShortOf()
    {
        // The 20,000 capstone is the only title fifty charts at a bare A cannot reach from the
        // top folder on either side. DOUBLE MASTER used to be the example and no longer is: the
        // Doubles A multiplier is interpolated rather than measured, and the value it currently
        // holds lifts a D29 ceiling just past that title's 19,500 ask.
        var rungs = Suggestion("ABYSS ABSOLUTE").Rungs;
        var last = rungs[^1];
        Assert.Equal(PhoenixLetterGrade.A, last.Grade);
        Assert.False(last.Reachable);
        Assert.Equal(new[] { "S29", "D29" }, last.Folders);

        Assert.All(rungs.Take(rungs.Count - 1), r => Assert.True(r.Reachable));
    }

    [Fact]
    public void AnUnreachableRungNeverMergesIntoTheOneAboveIt()
    {
        // Its folders name a ceiling rather than an answer, so folding it into a reachable run
        // would claim that ceiling serves.
        var rungs = Suggestion("ABYSS ABSOLUTE").Rungs;
        Assert.Equal(3, rungs.Count);
        Assert.All(rungs, r => Assert.False(r.OrBetter));
        Assert.Equal(new[] { "S29", "D29" }, rungs[^1].Folders);
        Assert.False(rungs[^1].Reachable);
    }

    [Fact]
    public void EveryTitleIsReachableAtTheGradeAboveTheOneThatFailsIt()
    {
        // A rung is only allowed to fall short at the bottom of the block — an unreachable grade
        // above a reachable one would mean the curve is not monotonic in grade.
        foreach (var title in Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>())
        {
            var reachable = SuggestedTitleLevel.For(title)!.Rungs.Select(r => r.Reachable).ToArray();
            Assert.Equal(reachable.OrderByDescending(r => r), reachable);
        }
    }
}
