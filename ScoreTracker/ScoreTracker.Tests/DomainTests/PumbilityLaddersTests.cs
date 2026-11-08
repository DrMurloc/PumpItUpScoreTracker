using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The PUMBILITY ladders per mix (docs/design/rise.md D27, D28): Rise keeps Phoenix 2's rungs and
///     raises the top of each ladder, Rise Arcade keeps Phoenix 2's numbers, and both keep their
///     titles against a list that holds those ladders and nothing else.
/// </summary>
public sealed class PumbilityLaddersTests
{
    private static readonly IReadOnlyDictionary<string, (int Phoenix2, int Rise)> Raised =
        new Dictionary<string, (int, int)>
        {
            ["[S] EXPERT LV.2"] = (17700, 17750),
            ["[S] EXPERT LV.3"] = (17900, 18000),
            ["[S] EXPERT LV.4"] = (18100, 18250),
            ["[S] EXPERT LV.5"] = (18300, 18500),
            ["[S] EXPERT LV.6"] = (18500, 18650),
            ["[S] EXPERT LV.7"] = (18600, 18750),
            ["[S] EXPERT LV.8"] = (18700, 18900),
            ["[S] EXPERT LV.9"] = (18800, 19000),
            ["[S] EXPERT LV.10"] = (18900, 19150),
            ["SINGLE MASTER"] = (19000, 19250),
            ["[D] EXPERT LV.2"] = (18200, 18250),
            ["[D] EXPERT LV.3"] = (18400, 18450),
            ["[D] EXPERT LV.4"] = (18600, 18750),
            ["[D] EXPERT LV.5"] = (18800, 19000),
            ["[D] EXPERT LV.6"] = (19000, 19150),
            ["[D] EXPERT LV.7"] = (19100, 19250),
            ["[D] EXPERT LV.8"] = (19200, 19400),
            ["[D] EXPERT LV.9"] = (19300, 19550),
            ["[D] EXPERT LV.10"] = (19400, 19650),
            ["DOUBLE MASTER"] = (19500, 19800),
            ["[P.B] ALEXANDRITE"] = (19000, 19200),
            ["ABYSS ABSOLUTE"] = (20000, 20100)
        };

    public static TheoryData<MixEnum> LadderMixes()
    {
        return new TheoryData<MixEnum>(MixEnum.Phoenix2, MixEnum.Rise, MixEnum.RiseArcade);
    }

    [Fact]
    public void RiseRaisesTheTopOfEachLadderAndKeepsEveryOtherRung()
    {
        var phoenix2 = PumbilityLadders.For(MixEnum.Phoenix2).ToDictionary(t => t.Name.ToString());
        var rise = PumbilityLadders.For(MixEnum.Rise).ToDictionary(t => t.Name.ToString());

        Assert.Equal(phoenix2.Keys.OrderBy(k => k), rise.Keys.OrderBy(k => k));
        foreach (var (name, title) in phoenix2)
            if (Raised.TryGetValue(name, out var moved))
            {
                Assert.Equal(moved.Phoenix2, title.CompletionRequired);
                Assert.Equal(moved.Rise, rise[name].CompletionRequired);
            }
            else
            {
                Assert.Equal(title.CompletionRequired, rise[name].CompletionRequired);
            }
    }

    [Fact]
    public void RiseArcadeKeepsEveryPhoenix2Number()
    {
        Assert.Equal(PumbilityLadders.For(MixEnum.Phoenix2).Select(t => (t.Name, t.Pool, t.CompletionRequired)),
            PumbilityLadders.For(MixEnum.RiseArcade).Select(t => (t.Name, t.Pool, t.CompletionRequired)));
    }

    [Theory]
    [MemberData(nameof(LadderMixes))]
    public void EachPoolClimbsAndEveryRungStartsWhereTheOneBelowFinished(MixEnum mix)
    {
        foreach (var pool in new[] { PumbilityPool.Singles, PumbilityPool.Doubles, PumbilityPool.Total })
        {
            var rungs = PumbilityLadders.For(mix, pool);
            Assert.NotEmpty(rungs);
            var floor = 0;
            foreach (var rung in rungs)
            {
                Assert.True(rung.CompletionRequired > floor);
                Assert.Equal(floor, rung.CompletionFloor);
                floor = rung.CompletionRequired;
            }
        }
    }

    [Fact]
    public void AMixOnPhoenixsRulesHasNoLadders()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PumbilityLadders.For(MixEnum.Phoenix));
    }

    [Theory]
    [InlineData(MixEnum.Rise, 19800)]
    [InlineData(MixEnum.RiseArcade, 19500)]
    public void TheRiseMixesKeepTheirTitlesAgainstTheLaddersAlone(MixEnum mix, int doubleMaster)
    {
        var list = TitleLists.For(mix);

        Assert.NotNull(list);
        var titles = list!.BuildList().ToArray();
        Assert.Equal(PumbilityLadders.For(MixEnum.Phoenix2).Count, titles.Length);
        Assert.All(titles, t => Assert.IsType<Phoenix2PumbilityTitle>(t));
        Assert.Equal(doubleMaster, list.GetTitleByName(Name.From("DOUBLE MASTER")).CompletionRequired);
    }

    [Fact]
    public void AMixThatKeepsNoTitlesHasNoList()
    {
        Assert.Null(TitleLists.For(MixEnum.XX));
        Assert.Null(TitleLists.For(MixEnum.Prime2));
        Assert.NotNull(TitleLists.For(MixEnum.Phoenix));
        Assert.NotNull(TitleLists.For(MixEnum.Phoenix2));
    }

    [Fact]
    public void RisesHalfDoublesFillTheDoublesLadderWhereTheyPayNothingOnPhoenix2()
    {
        // Twenty level-20 half-double Perfect Games: about 7,000 of doubles pool on Rise.
        var charts = Enumerable.Range(0, 20)
            .Select(_ => (Chart)new ChartBuilder().WithType(ChartType.HalfDouble).WithLevel(20).WithMix(MixEnum.Rise))
            .ToDictionary(c => c.Id);
        var scores = charts.Values.Select(c => new RecordedPhoenixScore(c.Id, PhoenixScore.From(1_000_000),
            PhoenixPlate.PerfectGame, false, DateTimeOffset.MinValue)).ToArray();

        var rise = TitleLists.For(MixEnum.Rise)!
            .BuildProgress(charts, scores, new HashSet<Name>())
            .ToDictionary(p => p.Title.Name.ToString());
        var pools = PumbilityLadderProgress.Pools(MixEnum.Phoenix2, charts, scores);

        Assert.True(rise["[D] INTERMEDIATE LV.2"].IsComplete);
        Assert.False(rise["[S] INTERMEDIATE LV.1"].IsComplete);
        Assert.False(rise["[P.B] BRONZE"].IsComplete);
        Assert.Equal(0, pools.Doubles);
    }
}
