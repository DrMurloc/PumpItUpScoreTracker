using System;
using System.Linq;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The rungs and the level crossing a batch's Hardmode pools moved through
///     (docs/design/hardmode-leaderboard.md D33). The numbers are DrMurloc's real 2026-09-15 session,
///     the one the Discord mock was drawn from.
/// </summary>
public sealed class HardmodeLaddersTests
{
    private static readonly DateTimeOffset When = new(2026, 9, 15, 11, 14, 0, TimeSpan.Zero);

    [Fact]
    public void EachPoolListsEveryRungItCrossedInLadderOrder()
    {
        var rungs = HardmodeLadders.RungsCrossed(MixEnum.Phoenix2, new[]
        {
            Gain(MilestoneKind.HardmodeDoublesPumbilityGain, 1343.72, 2027.18),
            Gain(MilestoneKind.HardmodeSinglesPumbilityGain, 1995.50, 9104.54),
            Gain(MilestoneKind.HardmodePumbilityGain, 3339.22, 11131.72)
        });

        // Combined first, then singles lowest rung first; doubles crossed nothing.
        Assert.Equal(new[]
        {
            "[P.B] BRONZE", "[S] INTERMEDIATE LV.1", "[S] INTERMEDIATE LV.2", "[S] INTERMEDIATE LV.3",
            "[S] INTERMEDIATE LV.4", "[S] INTERMEDIATE LV.5"
        }, rungs.Select(r => r.Title));
        Assert.Equal(PumbilityPool.Total, rungs[0].Pool);
        Assert.All(rungs.Skip(1), r => Assert.Equal(PumbilityPool.Singles, r.Pool));
    }

    [Fact]
    public void ARungIsCrossedWhenThePoolLandsExactlyOnIt()
    {
        var rungs = HardmodeLadders.RungsCrossed(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.HardmodePumbilityGain, 9999.99, 10000) });

        Assert.Equal("[P.B] BRONZE", Assert.Single(rungs).Title);
    }

    [Fact]
    public void APoolMovedBySeveralBatchesSpansThem()
    {
        // Earliest old, latest new — the rule the session page's Hardmode ladders follow.
        var rungs = HardmodeLadders.RungsCrossed(MixEnum.Phoenix2, new[]
        {
            Gain(MilestoneKind.HardmodePumbilityGain, 11800, 12600, When.AddMinutes(30)),
            Gain(MilestoneKind.HardmodePumbilityGain, 9800, 11800, When)
        });

        Assert.Equal(new[] { "[P.B] BRONZE", "[P.B] SILVER" }, rungs.Select(r => r.Title));
    }

    [Fact]
    public void RealPumbilityMilestonesAreNotHardmodeRungs()
    {
        var rungs = HardmodeLadders.RungsCrossed(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.PumbilityGain, 9800, 10200) });

        Assert.Empty(rungs);
    }

    [Fact]
    public void AMixWithoutTheGemLaddersHasNoRungs()
    {
        var rungs = HardmodeLadders.RungsCrossed(MixEnum.Phoenix,
            new[] { Gain(MilestoneKind.HardmodePumbilityGain, 9800, 10200) });

        Assert.Empty(rungs);
    }

    [Fact]
    public void TheCombinedPoolReportsALevelCrossingInsideAGem()
    {
        var change = HardmodeLadders.LevelChange(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.HardmodePumbilityGain, 10420, 11131.72) });

        Assert.NotNull(change);
        Assert.Equal("BRONZE LV.1 → LV.3", change!.CrossingText());
    }

    [Fact]
    public void ALevelCrossingIntoANewGemIsLeftToThatRungsOwnLine()
    {
        // 3,339 → 11,132 reaches BRONZE LV.3, but the batch also crossed BRONZE itself, and the
        // rung line already says so — PUMBILITY's own rule for its gem titles.
        var change = HardmodeLadders.LevelChange(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.HardmodePumbilityGain, 3339.22, 11131.72) });

        Assert.Null(change);
    }

    [Fact]
    public void AMoveInsideOneLevelIsNotACrossing()
    {
        var change = HardmodeLadders.LevelChange(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.HardmodePumbilityGain, 11100, 11400) });

        Assert.Null(change);
    }

    [Fact]
    public void OnlyTheCombinedPoolHasLevels()
    {
        var change = HardmodeLadders.LevelChange(MixEnum.Phoenix2,
            new[] { Gain(MilestoneKind.HardmodeSinglesPumbilityGain, 10420, 11131.72) });

        Assert.Null(change);
    }

    private static PlayerMilestoneRecord Gain(MilestoneKind kind, double from, double to,
        DateTimeOffset? at = null)
    {
        return new PlayerMilestoneRecord(kind, null, at ?? When, from, to, null, null);
    }
}
