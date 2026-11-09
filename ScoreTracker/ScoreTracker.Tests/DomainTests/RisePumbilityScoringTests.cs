using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Both RISE mixes price PUMBILITY with Phoenix 2's formula, read by Phoenix 2's rules
///     (docs/design/rise.md D29, D30). Rise's half-doubles are its doubles, so one prices at its
///     printed level the way a Phoenix 2 Double does; everything else matches Phoenix 2 cell for cell.
/// </summary>
public sealed class RisePumbilityScoringTests
{
    public static TheoryData<MixEnum> EveryMix()
    {
        return new TheoryData<MixEnum>(Enum.GetValues<MixEnum>());
    }

    [Theory]
    [MemberData(nameof(EveryMix))]
    public void AMixHasAFormulaExactlyWhenItsProfileHasPumbilityRules(MixEnum mix)
    {
        if (mix.HasPumbility())
            Assert.NotNull(ScoringConfiguration.PumbilityScoring(mix, false));
        else
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoringConfiguration.PumbilityScoring(mix, false));
    }

    [Fact]
    public void BothRiseMixesPlayByPhoenix2sRulesAndPhoenixKeepsItsOwn()
    {
        Assert.Equal(PumbilityRules.Phoenix2, MixProfiles.For(MixEnum.Rise).Pumbility);
        Assert.Equal(PumbilityRules.Phoenix2, MixProfiles.For(MixEnum.RiseArcade).Pumbility);
        Assert.Equal(PumbilityRules.Phoenix2, MixProfiles.For(MixEnum.Phoenix2).Pumbility);
        Assert.Equal(PumbilityRules.Phoenix, MixProfiles.For(MixEnum.Phoenix).Pumbility);
        Assert.Equal(PumbilityRules.None, MixProfiles.For(MixEnum.XX).Pumbility);
        Assert.False(MixEnum.Phoenix.UsesPhoenix2Pumbility());
        Assert.True(MixEnum.Phoenix.HasPumbility());
    }

    [Theory]
    [InlineData(10, 900_000, PhoenixPlate.RoughGame)]
    [InlineData(18, 975_000, PhoenixPlate.SuperbGame)]
    [InlineData(23, 991_234, PhoenixPlate.UltimateGame)]
    [InlineData(26, 1_000_000, PhoenixPlate.PerfectGame)]
    [InlineData(21, 812_000, PhoenixPlate.FairGame)]
    public void ARiseHalfDoublePricesAsAPhoenix2DoubleAtItsPrintedLevel(int level, int score, PhoenixPlate plate)
    {
        var rise = ScoringConfiguration.PumbilityScoring(MixEnum.Rise, false);
        var phoenix2 = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

        var halfDouble = rise.GetScore(ChartType.HalfDouble, DifficultyLevel.From(level), PhoenixScore.From(score),
            plate, false);

        Assert.True(halfDouble > 0);
        Assert.Equal(phoenix2.GetScore(ChartType.Double, DifficultyLevel.From(level), PhoenixScore.From(score),
            plate, false), halfDouble, 9);
    }

    [Theory]
    [InlineData(ChartType.Single, 17, 968_000, PhoenixPlate.MarvelousGame)]
    [InlineData(ChartType.Single, 25, 995_500, PhoenixPlate.ExtremeGame)]
    [InlineData(ChartType.Double, 22, 951_000, PhoenixPlate.TalentedGame)]
    public void EveryOtherCellMatchesPhoenix2(ChartType type, int level, int score, PhoenixPlate plate)
    {
        var phoenix2 = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
            .GetScore(type, DifficultyLevel.From(level), PhoenixScore.From(score), plate, false);

        foreach (var mix in new[] { MixEnum.Rise, MixEnum.RiseArcade })
            Assert.Equal(phoenix2, ScoringConfiguration.PumbilityScoring(mix, false)
                .GetScore(type, DifficultyLevel.From(level), PhoenixScore.From(score), plate, false), 9);
    }

    [Fact]
    public void ABrokenRiseHalfDoubleAndASubTenOnePriceAtNothing()
    {
        var rise = ScoringConfiguration.PumbilityScoring(MixEnum.Rise, false);

        Assert.Equal(0, rise.GetScore(ChartType.HalfDouble, DifficultyLevel.From(20), PhoenixScore.From(990_000),
            PhoenixPlate.SuperbGame, true));
        Assert.Equal(0, rise.GetScore(ChartType.HalfDouble, DifficultyLevel.From(9), PhoenixScore.From(990_000),
            PhoenixPlate.SuperbGame, false));
    }

    [Fact]
    public void APoolsTypesAreTheMixsOwnSinglesAndDoublesTypes()
    {
        Assert.Equal(new[] { ChartType.Single, ChartType.HalfDouble }, PumbilityPools.TypesOn(MixEnum.Rise).ToArray());
        Assert.Equal(new[] { ChartType.Single, ChartType.Double },
            PumbilityPools.TypesOn(MixEnum.RiseArcade).ToArray());
        Assert.Equal(new[] { ChartType.Single, ChartType.Double }, PumbilityPools.TypesOn(MixEnum.Phoenix2).ToArray());
    }
}
