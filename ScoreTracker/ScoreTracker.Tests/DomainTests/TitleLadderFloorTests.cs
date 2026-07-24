using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Models.Titles.XX;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Titles that read the same pool at rising thresholds are one ladder, and each rung
///     measures the climb from the rung below it. Earning Advanced Lv. 1 must not read as
///     a third of the way to Lv. 2 when the player has not touched a chart since.
/// </summary>
public sealed class TitleLadderFloorTests
{
    private static PhoenixTitle Phoenix(string name) => PhoenixTitleList.GetTitleByName(name);

    private static PhoenixTitle Phoenix2(string name) => Phoenix2TitleList.GetTitleByName(name);

    [Fact]
    public void FirstRungOfAPhoenixFolderStartsAtZero()
    {
        // 13,000 rating on 20s is the entry point for that folder — nothing precedes it.
        Assert.Equal(0, Phoenix("Advanced Lv. 1").CompletionFloor);
    }

    [Theory]
    [InlineData("Advanced Lv. 2", 13000)]
    [InlineData("Advanced Lv. 3", 26000)]
    [InlineData("Expert Lv. 2", 40000)]
    [InlineData("Expert Lv. 10", 3500)]
    public void LaterRungsOfAPhoenixFolderStartAtTheRungBelow(string title, int expectedFloor)
    {
        Assert.Equal(expectedFloor, Phoenix(title).CompletionFloor);
    }

    [Fact]
    public void PhoenixFoldersDoNotLinkToEachOther()
    {
        // Advanced Lv. 4 is the first 21s title; Advanced Lv. 3's 39,000 was on 20s and
        // counts for nothing here.
        Assert.Equal(0, Phoenix("Advanced Lv. 4").CompletionFloor);
    }

    [Fact]
    public void PhoenixCoOpTitlesAreOneLadder()
    {
        Assert.Equal(0, Phoenix("[CO-OP] Lv.1").CompletionFloor);
        Assert.Equal(30000, Phoenix("[CO-OP] Lv.2").CompletionFloor);
        Assert.Equal(360000, Phoenix("[CO-OP] MASTER").CompletionFloor);
    }

    [Fact]
    public void EveryPhoenix2PumbilityRungStartsAtTheOneBelowItInItsOwnPool()
    {
        Assert.Equal(0, Phoenix2("[S] INTERMEDIATE LV.1").CompletionFloor);
        Assert.Equal(5000, Phoenix2("[S] INTERMEDIATE LV.2").CompletionFloor);
        Assert.Equal(18900, Phoenix2("SINGLE MASTER").CompletionFloor);
        // The doubles ladder is its own pool — its Lv.2 floors on the doubles Lv.1.
        Assert.Equal(5000, Phoenix2("[D] INTERMEDIATE LV.2").CompletionFloor);
        Assert.Equal(10000, Phoenix2("[P.B] SILVER").CompletionFloor);
    }

    [Fact]
    public void Phoenix2SkillTitlesAreNotALadder()
    {
        // Each rung is a different chart at a fixed grade — a pass/fail, not an accumulation.
        Assert.Equal(0, Phoenix2("[TWIST S] LV.5").CompletionFloor);
    }

    [Fact]
    public void XxFoldersLinkOnTheirLevelRange()
    {
        var titles = XXTitleList.BuildProgress(Enumerable.Empty<BestXXChartAttempt>())
            .Select(p => p.Title)
            .OfType<XXDifficultyLevelTitle>()
            .ToDictionary(t => (string)t.Name);

        Assert.Equal(0, titles["Advanced LV.5"].CompletionFloor);
        Assert.Equal(20, titles["Advanced LV.6"].CompletionFloor);
        Assert.Equal(40, titles["Advanced LV.7"].CompletionFloor);
        // A new level range restarts the count.
        Assert.Equal(0, titles["Advanced LV.8"].CompletionFloor);
    }

    [Fact]
    public void ProgressMeasuresTheClimbBetweenRungs()
    {
        // Half of the 13,000 that separates Advanced Lv. 1 from Lv. 2, not half of 26,000.
        var progress = new PhoenixTitleProgress(Phoenix("Advanced Lv. 2"));
        progress.ApplyDirectProgress(19500);

        Assert.Equal(0.5, progress.PercentComplete, 3);
    }
}
