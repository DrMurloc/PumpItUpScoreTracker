using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class BestAttemptPolicyTests
{
    public static TheoryData<string, PhoenixScore?, PhoenixPlate?, bool, PhoenixScore?, PhoenixPlate?, bool, bool>
        Comparisons => new()
    {
        // stored                                    incoming                                   beats?
        { "higher score wins", 900000, PhoenixPlate.FairGame, false, 950000, PhoenixPlate.FairGame, false, true },
        { "lower score loses", 950000, PhoenixPlate.FairGame, false, 900000, PhoenixPlate.FairGame, false, false },
        { "identical is no change", 950000, PhoenixPlate.FairGame, false, 950000, PhoenixPlate.FairGame, false, false },
        // The bug this policy exists for: a plate improvement is not a personal best, and must
        // never carry a lower score into the record with it.
        { "better plate at a lower score loses", 950000, PhoenixPlate.FairGame, false, 900000,
            PhoenixPlate.SuperbGame, false, false },
        { "better plate at the same score wins", 950000, PhoenixPlate.FairGame, false, 950000,
            PhoenixPlate.SuperbGame, false, true },
        { "worse plate at the same score loses", 950000, PhoenixPlate.SuperbGame, false, 950000,
            PhoenixPlate.FairGame, false, false },
        { "worse plate at a higher score still wins", 900000, PhoenixPlate.PerfectGame, false, 950000,
            PhoenixPlate.RoughGame, false, true },
        // A pass outranks a break whatever the numbers, in both directions.
        { "a pass beats a higher break", 999999, null, true, 100000, PhoenixPlate.RoughGame, false, true },
        { "a break never beats a pass", 100000, PhoenixPlate.RoughGame, false, 999999, null, true, false },
        { "a better break beats a worse break", 100000, null, true, 200000, null, true, true },
        { "a worse break loses to a better break", 200000, null, true, 100000, null, true, false },
        // A scoreless record is the floor, not a tie: anything scored improves on it.
        { "any score beats no score", null, null, false, 1, PhoenixPlate.RoughGame, false, true },
        { "no score does not beat a score", 1, PhoenixPlate.RoughGame, false, null, null, false, false },
        { "no score does not beat no score", null, null, false, null, null, false, false }
    };

    [Theory]
    [MemberData(nameof(Comparisons))]
    public void BeatsFollowsPassThenScoreThenPlate(string because, PhoenixScore? storedScore,
        PhoenixPlate? storedPlate, bool storedIsBroken, PhoenixScore? incomingScore, PhoenixPlate? incomingPlate,
        bool incomingIsBroken, bool expected)
    {
        Assert.Equal(expected, BestAttemptPolicy.Beats(storedScore, storedPlate, storedIsBroken, incomingScore,
            incomingPlate, incomingIsBroken));
    }

    [Fact]
    public void AnythingBeatsNoRecordAtAll()
    {
        Assert.True(BestAttemptPolicy.Beats(null, 0, null, true));
        Assert.True(BestAttemptPolicy.Beats(null, null, null, false));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void PlateSurvivesOnlyOnAPass(bool isBroken, bool expectPlate)
    {
        Assert.Equal(expectPlate ? PhoenixPlate.SuperbGame : null,
            BestAttemptPolicy.PlateFor(isBroken, PhoenixPlate.SuperbGame));
    }

    [Fact]
    public void AWalkOffIsAZeroNoteBreak()
    {
        // Nothing judged at all — the play the owner described as "started a song and let it
        // fail out". Judged counts decide it when we have them.
        Assert.True(BestAttemptPolicy.IsWalkOff(true, 0, new JudgementCounts(0, 0, 0, 0, 0)));
        Assert.False(BestAttemptPolicy.IsWalkOff(true, 0, new JudgementCounts(0, 0, 0, 0, 51)));
    }

    [Fact]
    public void AZeroScoringBreakWithNoBreakdownIsAWalkOff()
    {
        // The redesigned best list carries no judgement table, so a zero score is the only
        // signal left.
        Assert.True(BestAttemptPolicy.IsWalkOff(true, 0, null));
        Assert.False(BestAttemptPolicy.IsWalkOff(true, 250000, null));
    }

    [Fact]
    public void APassIsNeverAWalkOffEvenAtZero()
    {
        Assert.False(BestAttemptPolicy.IsWalkOff(false, 0, new JudgementCounts(0, 0, 0, 0, 0)));
    }
}
