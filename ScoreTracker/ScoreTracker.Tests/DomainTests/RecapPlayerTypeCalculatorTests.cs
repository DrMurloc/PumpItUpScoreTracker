using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RecapPlayerTypeCalculatorTests
{
    [Fact]
    public void FewerThanTenScoresProducesNoType()
    {
        var scores = Enumerable.Repeat((PhoenixScore)999_000, 9).ToArray();

        Assert.Null(RecapPlayerTypeCalculator.Calculate(scores, MixEnum.Phoenix));
    }

    [Fact]
    public void TenScoresIsEnoughForAType()
    {
        var scores = Enumerable.Repeat((PhoenixScore)999_000, 10).ToArray();

        Assert.Equal(RecapPlayerType.Perfectionist, RecapPlayerTypeCalculator.Calculate(scores, MixEnum.Phoenix));
    }

    [Theory]
    [InlineData(949_999, RecapPlayerType.PassPusher)]
    [InlineData(950_000, RecapPlayerType.PassRefiner)]
    [InlineData(969_999, RecapPlayerType.PassRefiner)]
    [InlineData(970_000, RecapPlayerType.BalancedPlayer)]
    [InlineData(979_999, RecapPlayerType.BalancedPlayer)]
    [InlineData(980_000, RecapPlayerType.Competitive)]
    [InlineData(994_999, RecapPlayerType.Competitive)]
    [InlineData(995_000, RecapPlayerType.Perfectionist)]
    public void PhoenixBandsOnItsOwnAaaToSssPlusFloors(int score, RecapPlayerType expected)
    {
        var scores = Enumerable.Repeat((PhoenixScore)score, 10).ToArray();

        Assert.Equal(expected, RecapPlayerTypeCalculator.Calculate(scores, MixEnum.Phoenix));
    }

    // Phoenix 2 re-cut its grade table and packed the live range into four rungs, so its
    // archetypes are one grade each: S / S+ / SS / SS+ (docs/design/pumbility-overhaul.md D69).
    [Theory]
    [InlineData(969_999, RecapPlayerType.PassPusher)]
    [InlineData(970_000, RecapPlayerType.PassRefiner)]
    [InlineData(974_999, RecapPlayerType.PassRefiner)]
    [InlineData(975_000, RecapPlayerType.BalancedPlayer)]
    [InlineData(979_999, RecapPlayerType.BalancedPlayer)]
    [InlineData(980_000, RecapPlayerType.Competitive)]
    [InlineData(984_999, RecapPlayerType.Competitive)]
    [InlineData(985_000, RecapPlayerType.Perfectionist)]
    public void Phoenix2BandsOnItsOwnSToSsPlusFloors(int score, RecapPlayerType expected)
    {
        var scores = Enumerable.Repeat((PhoenixScore)score, 10).ToArray();

        Assert.Equal(expected, RecapPlayerTypeCalculator.Calculate(scores, MixEnum.Phoenix2));
    }

    // The two tables really do disagree across most of the live band, which is the whole reason
    // the mix has to be named: the same fifty is two archetypes apart at 985,000.
    [Theory]
    [InlineData(960_000, RecapPlayerType.PassRefiner, RecapPlayerType.PassPusher)]
    [InlineData(972_000, RecapPlayerType.BalancedPlayer, RecapPlayerType.PassRefiner)]
    [InlineData(977_000, RecapPlayerType.BalancedPlayer, RecapPlayerType.BalancedPlayer)]
    [InlineData(985_000, RecapPlayerType.Competitive, RecapPlayerType.Perfectionist)]
    public void TheSameAverageIsADifferentArchetypePerMix(int score, RecapPlayerType onPhoenix,
        RecapPlayerType onPhoenix2)
    {
        Assert.Equal(onPhoenix, RecapPlayerTypeCalculator.FromAverage(score, MixEnum.Phoenix));
        Assert.Equal(onPhoenix2, RecapPlayerTypeCalculator.FromAverage(score, MixEnum.Phoenix2));
    }

    // Every mix that is not Phoenix 2 bands on Phoenix's table. There is no third PUMBILITY
    // formula, so a caller reading an older mix is reading Phoenix's numbers by design.
    [Theory]
    [InlineData(MixEnum.Phoenix)]
    [InlineData(MixEnum.XX)]
    public void AnyMixButPhoenix2ReadsPhoenixsFloors(MixEnum mix)
    {
        Assert.Equal(RecapPlayerType.PassRefiner, RecapPlayerTypeCalculator.FromAverage(950_000, mix));
    }

    [Fact]
    public void TypeUsesTheAverageAcrossScores()
    {
        var scores = new PhoenixScore[]
            { 940_000, 960_000, 940_000, 960_000, 940_000, 960_000, 940_000, 960_000, 940_000, 960_000 };

        Assert.Equal(RecapPlayerType.PassRefiner, RecapPlayerTypeCalculator.Calculate(scores, MixEnum.Phoenix));
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.PassRefiner, 950_000)]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.Perfectionist, 995_000)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.PassRefiner, 970_000)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.Perfectionist, 985_000)]
    public void EveryTypeButThePassPusherKnowsItsFloor(MixEnum mix, RecapPlayerType type, int expected)
    {
        Assert.Equal(expected, RecapPlayerTypeCalculator.FloorFor(type, mix));
    }

    [Fact]
    public void ThePassPusherHasNoFloorBeneathIt()
    {
        Assert.Null(RecapPlayerTypeCalculator.FloorFor(RecapPlayerType.PassPusher, MixEnum.Phoenix2));
    }

    // The floors a mix bands on are that mix's own grade floors — the coincidence the constants
    // are chosen for, checked rather than assumed so a grade re-cut shows up here first.
    [Theory]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.PassRefiner, PhoenixLetterGrade.AAA)]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.BalancedPlayer, PhoenixLetterGrade.S)]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.Competitive, PhoenixLetterGrade.SS)]
    [InlineData(MixEnum.Phoenix, RecapPlayerType.Perfectionist, PhoenixLetterGrade.SSSPlus)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.PassRefiner, PhoenixLetterGrade.S)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.BalancedPlayer, PhoenixLetterGrade.SPlus)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.Competitive, PhoenixLetterGrade.SS)]
    [InlineData(MixEnum.Phoenix2, RecapPlayerType.Perfectionist, PhoenixLetterGrade.SSPlus)]
    public void EachFloorIsThatMixsOwnGradeFloor(MixEnum mix, RecapPlayerType type, PhoenixLetterGrade grade)
    {
        Assert.Equal((int)grade.GetMinimumScoreFor(mix), RecapPlayerTypeCalculator.FloorFor(type, mix));
    }
}
