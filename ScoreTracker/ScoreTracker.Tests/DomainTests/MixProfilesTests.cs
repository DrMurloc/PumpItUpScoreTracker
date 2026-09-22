using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class MixProfilesTests
{
    /// <summary>
    ///     The mixes that predate the profile. Their answers are pinned here as the rule the
    ///     one legacy boolean used to encode, so moving that rule onto the profile cannot change
    ///     a single one of them. A mix added after the profile lists its own expectations.
    /// </summary>
    private static readonly MixEnum[] PreProfileMixes =
    {
        MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2, MixEnum.FirstDanceFloor, MixEnum.SecondUltimateRemix,
        MixEnum.ThirdObg, MixEnum.ObgSeasonEvolution, MixEnum.Collection, MixEnum.PerfectCollection, MixEnum.Extra,
        MixEnum.Premiere, MixEnum.Prex, MixEnum.Rebirth, MixEnum.Premiere2, MixEnum.Prex2, MixEnum.Premiere3,
        MixEnum.Prex3, MixEnum.Exceed, MixEnum.Exceed2, MixEnum.Zero, MixEnum.Nx, MixEnum.Nx2, MixEnum.NxAbsolute,
        MixEnum.Fiesta, MixEnum.FiestaEx, MixEnum.Fiesta2, MixEnum.Prime, MixEnum.Prime2, MixEnum.Infinity,
        MixEnum.Pro, MixEnum.Pro2
    };

    public static IEnumerable<object[]> EveryMix()
    {
        return Enum.GetValues<MixEnum>().Select(m => new object[] { m });
    }

    public static IEnumerable<object[]> EveryPreProfileMix()
    {
        return PreProfileMixes.Select(m => new object[] { m });
    }

    [Theory]
    [MemberData(nameof(EveryMix))]
    public void EveryMixHasAProfile(MixEnum mix)
    {
        Assert.NotNull(MixProfiles.For(mix));
    }

    [Fact]
    public void ThePreProfileListNamesEveryMixThatExistedBeforeIt()
    {
        Assert.Equal(31, PreProfileMixes.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(EveryPreProfileMix))]
    public void APreProfileMixAnswersExactlyAsTheLegacyBooleanDid(MixEnum mix)
    {
        var phoenixGeneration = mix is MixEnum.Phoenix or MixEnum.Phoenix2;

        Assert.Equal(!phoenixGeneration, mix.UsesLegacyScoring());
        Assert.Equal(phoenixGeneration, mix.HasPumbility());
        Assert.Equal(phoenixGeneration, mix.HasOfficialBoards());
        Assert.Equal(phoenixGeneration, mix.HasWeeklyBoard());
        Assert.Equal(phoenixGeneration, mix.HasMarchOfMurlocs());
        Assert.Equal(phoenixGeneration, mix.HasPhoenixCalculators());
        Assert.Equal(phoenixGeneration, MixProfiles.For(mix).HasLifebarModel);
        Assert.Equal(phoenixGeneration ? mix : null, MixProfiles.For(mix).OfficialSite);
        Assert.Equal(phoenixGeneration ? AwardSet.PhoenixPlates : AwardSet.None, MixProfiles.For(mix).Awards);
        Assert.Equal(Platform.Pad, MixProfiles.For(mix).Platform);
    }

    [Fact]
    public void OnlyPhoenix2ReadsTheRecutGradeLadder()
    {
        foreach (var mix in PreProfileMixes)
            Assert.Equal(mix == MixEnum.Phoenix2 ? GradeLadder.Phoenix2 : GradeLadder.Phoenix1,
                MixProfiles.For(mix).GradeLadder);
    }

    [Fact]
    public void ThePhoenixGenerationDrawsItsOwnBubblesAndTheFlatScoreArt()
    {
        Assert.Equal(new MixArt("Phoenix", null, false), MixProfiles.For(MixEnum.Phoenix).Art);
        Assert.Equal(new MixArt("Phoenix2", null, true), MixProfiles.For(MixEnum.Phoenix2).Art);
        Assert.Equal(MixArt.Flat, MixProfiles.For(MixEnum.XX).Art);
        Assert.Equal(MixArt.Flat, MixProfiles.For(MixEnum.Prime2).Art);
    }
}
