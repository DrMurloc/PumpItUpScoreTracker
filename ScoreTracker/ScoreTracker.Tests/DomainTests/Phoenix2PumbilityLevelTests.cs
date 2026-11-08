using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The PUMBILITY level ladder, and the ratchet that keeps it honest. Two things can rot here and
///     neither would throw: the gem thresholds could move on the title list without moving here, and
///     a rung could drift a fraction off its round number. Both put a player on a badge they do not
///     wear, silently, so both are pinned.
/// </summary>
public sealed class Phoenix2PumbilityLevelTests
{
    /// <summary>
    ///     The drift ratchet. Every [P.B] gem's threshold is authored twice — once as a title, once
    ///     as the start of that gem's five levels — and the two must agree. This is the test that
    ///     fails when Andamiro re-prices a gem and only one of the two is updated.
    /// </summary>
    [Fact]
    public void EveryGemStartsWhereItsTitleSays()
    {
        var titles = Phoenix2TitleList.BuildList()
            .OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == PumbilityPool.Total)
            .OrderBy(t => t.CompletionRequired)
            .ToArray();

        var firstLevels = Phoenix2PumbilityLevel.All(MixEnum.Phoenix2)
            .Where(r => r.IsRanked && r.Level is null or 1)
            .OrderBy(r => r.Index)
            .ToArray();

        Assert.Equal(titles.Length, firstLevels.Length);
        foreach (var (title, rung) in titles.Zip(firstLevels))
        {
            Assert.Equal(title.Name, rung.Gem);
            Assert.Equal(title.CompletionRequired, rung.Threshold);
        }
    }

    [Fact]
    public void TheLadderRunsZeroToThirtySixWithoutAGap()
    {
        Assert.Equal(37, Phoenix2PumbilityLevel.All(MixEnum.Phoenix2).Count);
        Assert.Equal(Enumerable.Range(0, 37), Phoenix2PumbilityLevel.All(MixEnum.Phoenix2).Select(r => r.Index));
    }

    [Fact]
    public void EveryGemHasFiveLevelsExceptTheCapstone()
    {
        var byGem = Phoenix2PumbilityLevel.All(MixEnum.Phoenix2)
            .Where(r => r.IsRanked)
            .GroupBy(r => r.Gem!.Value.ToString())
            .ToArray();

        Assert.Equal(8, byGem.Length);
        foreach (var gem in byGem)
        {
            var expected = gem.Any(r => r.IsCapstone) ? 1 : Phoenix2PumbilityLevel.LevelsPerGem;
            Assert.Equal(expected, gem.Count());
        }
    }

    /// <summary>
    ///     Thresholds are authored round numbers, not measurements. A gem whose span did not divide
    ///     by five would produce fractional rungs, which is a shape error rather than an off-by-one.
    /// </summary>
    [Fact]
    public void EveryRungLandsOnAWholeStepOfItsGem()
    {
        foreach (var gem in Phoenix2PumbilityLevel.All(MixEnum.Phoenix2).Where(r => r.Level is not null)
                     .GroupBy(r => r.Gem!.Value.ToString()))
        {
            var rungs = gem.OrderBy(r => r.Level).ToArray();
            var step = rungs[1].Threshold - rungs[0].Threshold;
            Assert.All(rungs, r => Assert.Equal(rungs[0].Threshold + step * (r.Level!.Value - 1), r.Threshold));
            Assert.Equal(step * Phoenix2PumbilityLevel.LevelsPerGem, rungs[^1].NextThreshold - rungs[0].Threshold);
        }
    }

    [Fact]
    public void EveryThresholdRoundTripsToItsOwnRung()
    {
        foreach (var rung in Phoenix2PumbilityLevel.All(MixEnum.Phoenix2))
            Assert.Equal(rung.Index, Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, rung.Threshold).Index);
    }

    /// <summary>
    ///     The boundary is inclusive from below and nothing rounds across it. A hundredth under a
    ///     threshold is the rung beneath, which is exactly the case a rounded pool gets wrong.
    /// </summary>
    [Fact]
    public void AHairUnderAThresholdIsStillTheRungBeneath()
    {
        foreach (var rung in Phoenix2PumbilityLevel.All(MixEnum.Phoenix2).Where(r => r.Index > 0))
            Assert.Equal(rung.Index - 1, Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, rung.Threshold - 0.01).Index);
    }

    [Theory]
    [InlineData(17599.996, 3)] // would read LV.4 if the pool were rounded to the nearest whole
    [InlineData(17599.999999, 3)]
    [InlineData(17600.0, 4)]
    [InlineData(17602.69, 4)] // the account this ladder was decoded from
    public void APoolIsNeverRoundedIntoTheRungAbove(double pool, int expectedLevel)
    {
        var rung = Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, pool);
        Assert.Equal("[P.B] DIAMOND", rung.Gem!.Value.ToString());
        Assert.Equal(expectedLevel, rung.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4_000)]
    [InlineData(9_999.99)]
    public void ABelowBronzePoolIsUnrankedAndHoldsNoGem(double pool)
    {
        var rung = Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, pool);
        Assert.Equal(0, rung.Index);
        Assert.False(rung.IsRanked);
        Assert.Null(rung.Gem);
        Assert.Null(rung.Level);
        Assert.Equal(10_000, rung.NextThreshold);
    }

    [Fact]
    public void TheCapstoneIsTheTopAndHasNothingAbove()
    {
        var rung = Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, 20_039.72); // the ladder's only holder when this was decoded
        Assert.True(rung.IsCapstone);
        Assert.Equal("ABYSS ABSOLUTE", rung.Gem!.Value.ToString());
        Assert.Null(rung.Level);
        Assert.Null(rung.NextThreshold);
        Assert.Null(rung.ToNext(20_039.72));
    }

    [Fact]
    public void ToNextCountsDownToTheNextRungAndNeverBelowZero()
    {
        var rung = Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, 17_602.69);
        Assert.Equal(17_800, rung.NextThreshold);
        Assert.Equal(197.31, rung.ToNext(17_602.69)!.Value, 2);
        // A pool past this rung's own ceiling belongs to the rung above; asked anyway, it owes zero
        // rather than a negative distance.
        Assert.Equal(0, rung.ToNext(17_900));
    }

    [Fact]
    public void LevelsOfAGemComeBackWeakestFirst()
    {
        var diamond = Phoenix2PumbilityLevel.LevelsOf(MixEnum.Phoenix2, Name.From("[P.B] DIAMOND"));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, diamond.Select(r => r.Level!.Value));
        Assert.Equal(new[] { 17_000, 17_200, 17_400, 17_600, 17_800 }, diamond.Select(r => r.Threshold));
        Assert.Equal(Enumerable.Range(21, 5), diamond.Select(r => r.Index));
    }

    /// <summary>
    ///     The two gems the board cannot see are the ones most likely to be wrong, so their rungs are
    ///     spelled out rather than left to the general shape tests.
    /// </summary>
    [Fact]
    public void TheWideGemsStepFiveHundred()
    {
        Assert.Equal(new[] { 10_000, 10_500, 11_000, 11_500, 12_000 },
            Phoenix2PumbilityLevel.LevelsOf(MixEnum.Phoenix2, Name.From("[P.B] BRONZE")).Select(r => r.Threshold));
        Assert.Equal(new[] { 12_500, 13_000, 13_500, 14_000, 14_500 },
            Phoenix2PumbilityLevel.LevelsOf(MixEnum.Phoenix2, Name.From("[P.B] SILVER")).Select(r => r.Threshold));
    }

    /// <summary>Rise's raised ALEXANDRITE and ABYSS ABSOLUTE widen the gems below them (rise.md D27).</summary>
    [Fact]
    public void RisesRaisedGemsMoveItsLevelsWithThem()
    {
        Assert.Equal(new[] { 18_000, 18_240, 18_480, 18_720, 18_960 },
            Phoenix2PumbilityLevel.LevelsOf(MixEnum.Rise, Name.From("[P.B] RED BERYL")).Select(r => r.Threshold));
        Assert.Equal(new[] { 19_200, 19_380, 19_560, 19_740, 19_920 },
            Phoenix2PumbilityLevel.LevelsOf(MixEnum.Rise, Name.From("[P.B] ALEXANDRITE")).Select(r => r.Threshold));
        Assert.Equal(20_100, Phoenix2PumbilityLevel.All(MixEnum.Rise)[^1].Threshold);
        Assert.Equal(37, Phoenix2PumbilityLevel.All(MixEnum.Rise).Count);
    }

    [Fact]
    public void OnePoolStandsOnDifferentRungsOnRiseAndPhoenix2()
    {
        var rise = Phoenix2PumbilityLevel.From(MixEnum.Rise, 19_100);
        var phoenix2 = Phoenix2PumbilityLevel.From(MixEnum.Phoenix2, 19_100);

        Assert.Equal((Name?)Name.From("[P.B] RED BERYL"), rise.Gem);
        Assert.Equal(5, rise.Level);
        Assert.Equal((Name?)Name.From("[P.B] ALEXANDRITE"), phoenix2.Gem);
        Assert.Equal(1, phoenix2.Level);
        Assert.Equal(rise.Index + 1, phoenix2.Index);
    }

    [Fact]
    public void RiseArcadesLadderIsPhoenix2s()
    {
        Assert.Equal(Phoenix2PumbilityLevel.All(MixEnum.Phoenix2).Select(r => (r.Index, r.Threshold)),
            Phoenix2PumbilityLevel.All(MixEnum.RiseArcade).Select(r => (r.Index, r.Threshold)));
    }

    [Fact]
    public void AMixWithoutTheLadderCannotAskForARung()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Phoenix2PumbilityLevel.From(MixEnum.Phoenix, 15_000));
    }
}
