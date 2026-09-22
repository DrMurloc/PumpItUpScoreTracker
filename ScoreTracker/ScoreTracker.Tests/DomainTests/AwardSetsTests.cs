using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class AwardSetsTests
{
    [Fact]
    public void APhoenixMixAwardsAllEightPlatesUnderTheirOwnNames()
    {
        Assert.Equal(Enum.GetValues<PhoenixPlate>(), MixEnum.Phoenix2.AwardsOf());
        Assert.Equal("UG", PhoenixPlate.UltimateGame.GetShorthand(MixEnum.Phoenix2));
        Assert.Equal("Ultimate Game", PhoenixPlate.UltimateGame.GetName(MixEnum.Phoenix2));
        Assert.Equal(PhoenixPlate.TalentedGame, AwardSets.TryParseShorthand("tg", MixEnum.Phoenix));
    }

    [Fact]
    public void RiseAwardsThreeMarksStoredAsThePlatesTheyCoincideWith()
    {
        Assert.Equal(new[] { PhoenixPlate.SuperbGame, PhoenixPlate.UltimateGame, PhoenixPlate.PerfectGame },
            MixEnum.Rise.AwardsOf());
        Assert.Equal("PG", PhoenixPlate.PerfectGame.GetShorthand(MixEnum.Rise));
        Assert.Equal("FC", PhoenixPlate.UltimateGame.GetShorthand(MixEnum.Rise));
        Assert.Equal("NM", PhoenixPlate.SuperbGame.GetShorthand(MixEnum.Rise));
        Assert.Equal("Full Combo", PhoenixPlate.UltimateGame.GetName(MixEnum.Rise));
        Assert.Equal("No Miss", PhoenixPlate.SuperbGame.GetName(MixEnum.Rise));
    }

    [Theory]
    [InlineData("fc", PhoenixPlate.UltimateGame)]
    [InlineData("NM", PhoenixPlate.SuperbGame)]
    [InlineData(" pg ", PhoenixPlate.PerfectGame)]
    [InlineData("UG", PhoenixPlate.UltimateGame)]
    [InlineData("sg", PhoenixPlate.SuperbGame)]
    public void ARiseMarkReadsAsItsOwnShorthandOrTheStoredPlateCode(string written, PhoenixPlate expected)
    {
        Assert.Equal(expected, AwardSets.TryParseShorthand(written, MixEnum.Rise));
    }

    [Theory]
    [InlineData("TG")]
    [InlineData("RG")]
    [InlineData("EG")]
    [InlineData("")]
    [InlineData("Perfect")]
    public void RiseRefusesAPlateItDoesNotAward(string written)
    {
        Assert.Null(AwardSets.TryParseShorthand(written, MixEnum.Rise));
        Assert.Throws<ArgumentOutOfRangeException>(() => AwardSets.ParseShorthand(written, MixEnum.Rise));
    }

    [Fact]
    public void RiseArcadeAwardsThePhoenixPlatesAndALegacyMixAwardsNothing()
    {
        Assert.Equal(8, MixEnum.RiseArcade.AwardsOf().Count);
        Assert.Equal("FC", PhoenixPlate.UltimateGame.GetShorthand(MixEnum.Rise));
        Assert.Equal("UG", PhoenixPlate.UltimateGame.GetShorthand(MixEnum.RiseArcade));
        Assert.Empty(MixEnum.Prime2.AwardsOf());
        Assert.Null(AwardSets.TryParseShorthand("PG", MixEnum.Prime2));
    }
}
