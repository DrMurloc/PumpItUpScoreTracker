using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ArchetypeSpreadTests
{
    private const MixEnum Mix = MixEnum.Phoenix2;

    /// <summary>
    ///     A cohort of holders at given averages. Each gets a pool of <paramref name="poolSize" />
    ///     charts — a real one by default, since a holder under the sample-size guard is deliberately
    ///     not banded at all.
    /// </summary>
    private static PeerPoolSummary Summary(int poolSize, params (double Average, bool FromBoard)[] holders)
    {
        var peers = new HashSet<PeerVoice>();
        var averages = new Dictionary<PeerVoice, double>();
        var pools = new Dictionary<PeerVoice, IReadOnlySet<Guid>>();
        for (var i = 0; i < holders.Length; i++)
        {
            var voice = holders[i].FromBoard
                ? PeerVoice.FromBoard(i + 1, $"board{i + 1}")
                : PeerVoice.Account(Guid.NewGuid());
            peers.Add(voice);
            averages[voice] = holders[i].Average;
            pools[voice] = Enumerable.Range(0, poolSize).Select(_ => Guid.NewGuid()).ToHashSet();
        }

        return new PeerPoolSummary(peers, pools, new Dictionary<Guid, PeerPoolChart>(), null, averages);
    }

    private static PeerPoolSummary Summary(params (double Average, bool FromBoard)[] holders) =>
        Summary(50, holders);

    private static int[] Pool(int score, int count = 50)
    {
        return Enumerable.Repeat(score, count).ToArray();
    }

    [Fact]
    public void EveryArchetypeIsPresentEvenWhereNobodyStandsInIt()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((982_000, false)), Mix);

        Assert.Equal(Enum.GetValues<RecapPlayerType>().Length, cohort.HoldersByArchetype.Count);
        Assert.Equal(0, cohort.HoldersByArchetype[RecapPlayerType.PassPusher]);
        Assert.Equal(1, cohort.HoldersByArchetype[RecapPlayerType.Competitive]);
    }

    [Fact]
    public void HoldersAreBandedOnTheMixsOwnCutoffs()
    {
        var holders = new[] { (968_000d, false), (972_000d, false), (977_000d, false), (982_000d, false), (988_000d, false) };

        var onPhoenix2 = CohortArchetypeSpread.Of(Summary(holders), MixEnum.Phoenix2);
        var onPhoenix = CohortArchetypeSpread.Of(Summary(holders), MixEnum.Phoenix);

        Assert.Equal(new[] { 1, 1, 1, 1, 1 }, Counts(onPhoenix2));
        // The same five on Phoenix's floors: nobody is a Pusher and nobody reaches SSS+, so the
        // spread the archetypes are meant to describe collapses into the middle three.
        Assert.Equal(new[] { 0, 1, 2, 2, 0 }, Counts(onPhoenix));
    }

    [Fact]
    public void AHolderWithNoMeasuredFiftyIsNotCounted()
    {
        var voice = PeerVoice.Account(Guid.NewGuid());
        var summary = new PeerPoolSummary(new HashSet<PeerVoice> { voice },
            new Dictionary<PeerVoice, IReadOnlySet<Guid>>(), new Dictionary<Guid, PeerPoolChart>());

        var cohort = CohortArchetypeSpread.Of(summary, Mix);

        Assert.Equal(0, cohort.Holders);
        Assert.All(cohort.HoldersByArchetype.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void TheViewersOwnBandIsReadOffTheirOwnFifty()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((972_000, false), (982_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(977_000), Mix);

        Assert.Equal(RecapPlayerType.BalancedPlayer, spread.Mine);
        Assert.Equal(977_000, spread.MyAverage);
    }

    [Fact]
    public void TooShortAPoolLeavesTheMarkerOffAndTheCohortDrawn()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((982_000, false), (983_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(977_000, RecapPlayerTypeCalculator.MinimumScores - 1), Mix);

        Assert.Null(spread.Mine);
        Assert.Null(spread.MyAverage);
        Assert.Null(spread.Ahead);
        Assert.Equal(2, spread.Holders);
    }

    [Fact]
    public void StandingWithMeCountsTheViewersOwnBand()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((977_000, false), (978_000, false), (982_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(976_000), Mix);

        Assert.Equal(RecapPlayerType.BalancedPlayer, spread.Mine);
        Assert.Equal(2, spread.StandingWithMe);
    }

    [Fact]
    public void NobodySharingTheViewersBandIsZeroRatherThanAnError()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((972_000, false), (973_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(988_000), Mix);

        Assert.Equal(RecapPlayerType.Perfectionist, spread.Mine);
        Assert.Equal(0, spread.StandingWithMe);
    }

    [Fact]
    public void SharesAreOverTheBandedHolders()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((972_000, false), (977_000, false),
            (978_000, false), (982_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(977_000), Mix);

        Assert.Equal(0.25, spread.ShareOf(RecapPlayerType.PassRefiner));
        Assert.Equal(0.50, spread.ShareOf(RecapPlayerType.BalancedPlayer));
        Assert.Equal(0, spread.ShareOf(RecapPlayerType.PassPusher));
    }

    [Fact]
    public void AnEmptyCohortSharesNothingRatherThanDividingByZero()
    {
        var spread = ArchetypeSpread.Of(CohortArchetypeSpread.Empty, Pool(977_000), Mix);

        Assert.Equal(0, spread.ShareOf(RecapPlayerType.BalancedPlayer));
        Assert.Null(spread.Ahead);
    }

    // The marker has to land inside the band it belongs to, or the drawing says one archetype and
    // outlines another — which is the whole failure the section exists to avoid.
    [Theory]
    [InlineData(968_000, RecapPlayerType.PassPusher)]
    [InlineData(970_000, RecapPlayerType.PassRefiner)]
    [InlineData(974_999, RecapPlayerType.PassRefiner)]
    [InlineData(977_500, RecapPlayerType.BalancedPlayer)]
    [InlineData(984_000, RecapPlayerType.Competitive)]
    [InlineData(991_000, RecapPlayerType.Perfectionist)]
    public void TheMarkerFallsInsideItsOwnBand(int average, RecapPlayerType expected)
    {
        var cohort = CohortArchetypeSpread.Of(Summary((968_000, false), (972_000, false), (977_000, false),
            (982_000, false), (988_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(average), Mix);

        Assert.Equal(expected, spread.Mine);
        var below = Enum.GetValues<RecapPlayerType>().Where(t => t < expected).Sum(spread.ShareOf);
        Assert.InRange(spread.Ahead!.Value, below, below + spread.ShareOf(expected));
    }

    [Fact]
    public void BoardHoldersAreBandedLikeAnybodyElse()
    {
        var cohort = CohortArchetypeSpread.Of(Summary((977_000, false), (982_000, true), (983_000, true)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(977_000), Mix);

        Assert.Equal(3, spread.Holders);
        Assert.Equal(2, spread.HoldersByArchetype[RecapPlayerType.Competitive]);
    }

    [Fact]
    public void AHolderUnderTheSampleSizeGuardIsNotBanded()
    {
        // The same guard the chip and the viewer's own marker apply: below it an average says more
        // about how much has been played than about how it was played.
        var cohort = CohortArchetypeSpread.Of(
            Summary(RecapPlayerTypeCalculator.MinimumScores - 1, (982_000, false), (983_000, false)), Mix);

        Assert.Equal(0, cohort.Holders);
        Assert.All(cohort.HoldersByArchetype.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void TheMarkerNeverSitsOnEitherEndOfTheStrip()
    {
        // Nobody shares the viewer's archetype and everyone sits below them — a low gem's
        // Perfectionist, which §4.15 measured four of. Unclamped that is exactly 1.0, and the
        // marker is centred on its position, so half the diamond would hang off the strip.
        var cohort = CohortArchetypeSpread.Of(Summary((972_000, false), (973_000, false)), Mix);

        var spread = ArchetypeSpread.Of(cohort, Pool(988_000), Mix);

        Assert.InRange(spread.Ahead!.Value, 0.5, 0.995);
    }

    private static int[] Counts(CohortArchetypeSpread cohort)
    {
        return Enum.GetValues<RecapPlayerType>().Select(type => cohort.HoldersByArchetype[type]).ToArray();
    }
}
