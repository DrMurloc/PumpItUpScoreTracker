using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Phoenix 2's pumbility carries two rules of its own — a chart below level 10 pays nothing,
///     and a Single's base is bumped +5 (+10 from level 25) — that used to ride on
///     <c>Mix == Phoenix2</c>. They are configuration flags now (docs/design/march-of-murlocs.md
///     §9.2), because PUMBILITY+ on Phoenix 2 must grade on Phoenix 2's cutoffs (so it carries
///     <c>Mix = Phoenix2</c>) without inheriting either rule. The golden rows in
///     <see cref="Phoenix2PumbilityScoringTests" /> pin the stock output unchanged; these pin the
///     switch itself.
/// </summary>
public sealed class ScoringConfigurationPhoenix2RulesTests
{
    private static readonly PhoenixScore Aaa = 950000;

    [Fact]
    public void OnlyTheStockPhoenix2PumbilityTurnsTheTwoRulesOn()
    {
        var phoenix2 = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var phoenix = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var plus = ScoringConfiguration.PumbilityPlus;

        Assert.True(phoenix2.ZeroBelowLevelTen);
        Assert.True(phoenix2.SinglesLevelBump);
        Assert.False(phoenix.ZeroBelowLevelTen);
        Assert.False(phoenix.SinglesLevelBump);
        Assert.False(plus.ZeroBelowLevelTen);
        Assert.False(plus.SinglesLevelBump);
    }

    [Fact]
    public void TheStockRulesStillFireWhenOn()
    {
        var stock = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        Assert.Equal(0, stock.GetScore(ChartType.Double, DifficultyLevel.From(9), Aaa, PhoenixPlate.RoughGame));
        var single = stock.GetScore(ChartType.Single, DifficultyLevel.From(20), Aaa, PhoenixPlate.RoughGame);
        var @double = stock.GetScore(ChartType.Double, DifficultyLevel.From(20), Aaa, PhoenixPlate.RoughGame);
        Assert.True(single > @double, "a Single is bumped above the Double at the same level and score");
    }

    [Fact]
    public void APhoenix2ConfigWithTheRulesOffPricesLevelNineAndLeavesSinglesAlone()
    {
        // PUMBILITY+ on Phoenix 2: the Phoenix builder verbatim, graded on Phoenix 2's cutoffs.
        var plus = ScoringConfiguration.PumbilityPlus;
        plus.Mix = MixEnum.Phoenix2;

        Assert.False(plus.ZeroBelowLevelTen);
        Assert.False(plus.SinglesLevelBump);
        Assert.True(plus.GetScore(ChartType.Double, DifficultyLevel.From(9), Aaa, PhoenixPlate.RoughGame) > 0,
            "levels 1–9 keep their 10…90 ratings");
        Assert.Equal(
            plus.GetScore(ChartType.Double, DifficultyLevel.From(20), Aaa, PhoenixPlate.RoughGame),
            plus.GetScore(ChartType.Single, DifficultyLevel.From(20), Aaa, PhoenixPlate.RoughGame));
    }

    [Fact]
    public void TheFlagsDecideNotTheMix()
    {
        // The stock Phoenix 2 config with both rules switched off: still Phoenix 2, still the
        // grade-plus-plate formula, and neither rule fires — the branches read the flags alone.
        var off = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        off.ZeroBelowLevelTen = false;
        off.SinglesLevelBump = false;

        Assert.True(off.GetScore(ChartType.Double, DifficultyLevel.From(5), Aaa, PhoenixPlate.RoughGame) > 0,
            "a level 5 prices on the base curve once the exclusion is off");
        Assert.Equal(
            off.GetScore(ChartType.Double, DifficultyLevel.From(26), Aaa, PhoenixPlate.RoughGame),
            off.GetScore(ChartType.Single, DifficultyLevel.From(26), Aaa, PhoenixPlate.RoughGame));
    }
}
