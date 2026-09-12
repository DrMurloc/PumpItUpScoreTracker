using System.Linq;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The bands the Breakdown card's comparisons are drawn from (docs/design/pumbility-overhaul.md
///     D68): the merged ladder's gems and their levels, and the typed ladders' rungs.
/// </summary>
public sealed class PumbilityBandTests
{
    [Fact]
    public void TheMergedLadderIsFiveLevelsAGemAndOneCapstone()
    {
        var levels = PumbilityBand.Levels(PumbilityPool.Total);
        Assert.Equal(36, levels.Count);

        var first = levels[0];
        Assert.Equal("[P.B] BRONZE LV.1", first.Name.ToString());
        Assert.Equal(10_000, first.Floor);
        Assert.Equal(10_500, first.Ceiling);

        var capstone = levels[^1];
        Assert.Equal("ABYSS ABSOLUTE", capstone.Name.ToString());
        Assert.Equal(20_000, capstone.Floor);
        Assert.Null(capstone.Ceiling);
    }

    [Fact]
    public void AMergedLevelKnowsTheGemAroundIt()
    {
        // The owner's own pool on 2026-09-11.
        var level = PumbilityBand.LevelOf(PumbilityPool.Total, 17_738.06);
        Assert.NotNull(level);
        Assert.Equal("[P.B] DIAMOND LV.4", level!.Name.ToString());
        Assert.Equal(17_600, level.Floor);
        Assert.Equal(17_800, level.Ceiling);
        Assert.Equal("[P.B] DIAMOND", level.Gem!.Value.ToString());

        var gem = PumbilityBand.GemOf(17_738.06);
        Assert.NotNull(gem);
        Assert.Equal("[P.B] DIAMOND", gem!.Name.ToString());
        Assert.Equal(17_000, gem.Floor);
        Assert.Equal(18_000, gem.Ceiling);
        Assert.Null(gem.Gem);
    }

    [Fact]
    public void TheTypedLaddersAreRungsWithNothingCoarserInside()
    {
        var singles = PumbilityBand.Levels(PumbilityPool.Singles);
        Assert.Equal(31, singles.Count);
        Assert.All(singles, band => Assert.Null(band.Gem));

        var rung = PumbilityBand.LevelOf(PumbilityPool.Singles, 16_100);
        Assert.Equal("[S] ADVANCED LV.5", rung!.Name.ToString());
        Assert.Equal(16_000, rung.Floor);
        Assert.Equal(16_250, rung.Ceiling);

        var master = PumbilityBand.Levels(PumbilityPool.Doubles)[^1];
        Assert.Equal("DOUBLE MASTER", master.Name.ToString());
        Assert.Equal(19_500, master.Floor);
        Assert.Null(master.Ceiling);
    }

    [Fact]
    public void EveryLadderIsContiguousFromItsFloorToItsRoof()
    {
        foreach (var pool in new[] { PumbilityPool.Total, PumbilityPool.Singles, PumbilityPool.Doubles })
        {
            var bands = PumbilityBand.Levels(pool);
            for (var i = 0; i + 1 < bands.Count; i++)
                Assert.Equal(bands[i + 1].Floor, bands[i].Ceiling);
            Assert.Null(bands[^1].Ceiling);
        }

        var gems = PumbilityBand.Gems();
        Assert.Equal(8, gems.Count);
        for (var i = 0; i + 1 < gems.Count; i++) Assert.Equal(gems[i + 1].Floor, gems[i].Ceiling);
    }

    [Fact]
    public void APoolUnderTheLadderStandsOnNoBand()
    {
        Assert.Null(PumbilityBand.LevelOf(PumbilityPool.Total, 9_999.99));
        Assert.Null(PumbilityBand.GemOf(9_999.99));
        Assert.Null(PumbilityBand.LevelOf(PumbilityPool.Singles, 4_999));
        Assert.Null(PumbilityBand.LevelOf(PumbilityPool.Doubles, 0));
    }

    [Fact]
    public void ASavedChoiceIsFoundByName()
    {
        Assert.Equal(17_600, PumbilityBand.ByName(PumbilityPool.Total, "[P.B] DIAMOND LV.4")!.Floor);
        // A gem is a band a viewer can pin too, even though it is not one of the levels.
        Assert.Equal(17_000, PumbilityBand.ByName(PumbilityPool.Total, "[P.B] DIAMOND")!.Floor);
        Assert.Equal(17_500, PumbilityBand.ByName(PumbilityPool.Singles, "[S] EXPERT LV.1")!.Floor);
        Assert.Null(PumbilityBand.ByName(PumbilityPool.Singles, "[P.B] DIAMOND"));
        Assert.Null(PumbilityBand.ByName(PumbilityPool.Total, "nothing of the sort"));
    }

    [Fact]
    public void TwentyFiveIsWhatALevelOwesBeforeItIsReadOverItsGem()
    {
        Assert.Equal(25, PumbilityBand.MinimumForLevel);
    }
}
