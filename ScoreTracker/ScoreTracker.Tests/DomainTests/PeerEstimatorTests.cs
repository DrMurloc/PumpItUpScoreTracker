using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Services;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PeerEstimatorTests
{
    private static PeerScore Peer(int score, double growth = 0) => new(score, 20.0, 20.0 - growth);

    [Fact]
    public void NoPeersMeansNoOpinion()
    {
        Assert.Null(PeerEstimator.Estimate(Array.Empty<PeerScore>()));
    }

    [Fact]
    public void ASinglePeerIsTheEstimateAtEveryQuantile()
    {
        var one = new[] { Peer(950_000) };
        Assert.Equal(950_000, PeerEstimator.Estimate(one, quantile: 0.0));
        Assert.Equal(950_000, PeerEstimator.Estimate(one, quantile: 0.65));
        Assert.Equal(950_000, PeerEstimator.Estimate(one, quantile: 1.0));
    }

    [Fact]
    public void TheEstimateSitsAboveTheMeanOnALeftSkewedGroup()
    {
        // The shape every real chart has: a cluster of good scores plus a tail of
        // barely-passed attempts. A mean lands in the tail; p65 lands in the cluster.
        var peers = new[]
        {
            Peer(600_000), Peer(720_000), Peer(910_000), Peer(945_000), Peer(960_000),
            Peer(965_000), Peer(970_000), Peer(975_000), Peer(980_000), Peer(985_000)
        };
        var mean = peers.Average(p => p.Score);
        var estimate = PeerEstimator.Estimate(peers)!.Value;

        Assert.True(estimate > mean,
            $"expected p65 ({estimate:N0}) above the mean ({mean:N0}) on a left-skewed group");
        Assert.True(estimate >= 960_000, $"expected the estimate inside the cluster, got {estimate:N0}");
    }

    [Fact]
    public void RaisingTheQuantileRaisesTheEstimateMonotonically()
    {
        var peers = Enumerable.Range(0, 20).Select(i => Peer(900_000 + i * 5_000)).ToArray();
        var previous = int.MinValue;
        foreach (var q in new[] { 0.1, 0.3, 0.5, 0.65, 0.8, 0.95 })
        {
            var estimate = PeerEstimator.Estimate(peers, quantile: q)!.Value;
            Assert.True(estimate >= previous, $"q={q} produced {estimate:N0} below the previous {previous:N0}");
            previous = estimate;
        }
    }

    [Fact]
    public void APlayerWhoNeverLevelledCountsAtFullVoice()
    {
        Assert.Equal(1.0, PeerEstimator.GrowthWeight(0), 6);
    }

    [Fact]
    public void GrowthWeightFallsAsTheOwnerOutgrowsTheScore()
    {
        var flat = PeerEstimator.GrowthWeight(0);
        var oneLevel = PeerEstimator.GrowthWeight(1);
        var twoLevels = PeerEstimator.GrowthWeight(2);

        Assert.True(oneLevel < flat);
        Assert.True(twoLevels < oneLevel);
        Assert.Equal(Math.Exp(-1), oneLevel, 6);
    }

    [Fact]
    public void ALevelDropDoesNotDiscountAScore()
    {
        // Falling below where you were does not make a past score less representative;
        // only growth does. Guards against a signed subtraction leaking through.
        Assert.Equal(1.0, PeerEstimator.GrowthWeight(-3), 6);
        Assert.Equal(PeerEstimator.Estimate(new[] { new PeerScore(950_000, 18.0, 22.0) }),
            PeerEstimator.Estimate(new[] { new PeerScore(950_000, 22.0, 22.0) }));
    }

    [Fact]
    public void GrownPeersPullTheEstimateTowardTheCurrentOnes()
    {
        // Two populations: stale low scorers and current high scorers. Discounting the
        // stale half must move the estimate up.
        var stale = Enumerable.Range(0, 6).Select(i => new PeerScore(900_000 + i * 1_000, 22.0, 19.0));
        var current = Enumerable.Range(0, 6).Select(i => new PeerScore(970_000 + i * 1_000, 22.0, 22.0));
        var peers = stale.Concat(current).ToArray();

        var weighted = PeerEstimator.Estimate(peers)!.Value;
        var unweighted = PeerEstimator.Estimate(peers, growthDecayLevels: 0)!.Value;

        Assert.True(weighted > unweighted,
            $"growth weighting should favour current scores: {weighted:N0} vs {unweighted:N0}");
    }

    [Fact]
    public void AStableGroupIsUnaffectedByGrowthWeighting()
    {
        // The self-conditioning property: nobody grew, so the weight is inert and the
        // estimate matches the unweighted one exactly.
        var peers = Enumerable.Range(0, 12).Select(i => Peer(930_000 + i * 4_000)).ToArray();

        Assert.Equal(PeerEstimator.Estimate(peers, growthDecayLevels: 0),
            PeerEstimator.Estimate(peers));
    }

    [Fact]
    public void TheWeightingCountsVoicesNotHeads()
    {
        // Three peers, two of them badly outgrown, are worth about one voice between them.
        // The page no longer prints that number, but every quantile Estimate reads is taken
        // over these weights, so the property still has to hold.
        var voices = new[] { 0.0, 3.0, 3.0 }.Sum(g => PeerEstimator.GrowthWeight(g));

        Assert.True(voices < 1.2, $"three peers, two badly outgrown, should be worth ~1 voice; got {voices:N2}");
    }

    [Fact]
    public void TheEstimateNeverLeavesTheObservedRange()
    {
        var peers = new[] { Peer(880_000), Peer(915_000, growth: 2), Peer(1_000_000, growth: 0.5) };

        var estimate = PeerEstimator.Estimate(peers)!.Value;

        Assert.InRange(estimate, 880_000, 1_000_000);
    }

    [Fact]
    public void FewerPeersThanTheFloorIsNoOpinion()
    {
        // Phoenix 2 asks for five (§4.8, D24). Four peers, however confident, is nothing;
        // the fifth is an estimate.
        var four = Enumerable.Range(0, 4).Select(i => Peer(970_000 + i * 1_000)).ToArray();
        var five = four.Append(Peer(974_000)).ToArray();

        Assert.Null(PeerEstimator.Estimate(four, minimumPeers: PeerEstimator.Phoenix2MinimumPeers));
        Assert.NotNull(PeerEstimator.Estimate(five, minimumPeers: PeerEstimator.Phoenix2MinimumPeers));
    }

    [Fact]
    public void TheDefaultFloorIsOnePeerSoPhoenixOneIsUnchanged()
    {
        Assert.Equal(950_000, PeerEstimator.Estimate(new[] { Peer(950_000) }));
        // A floor below one is treated as one: zero peers is never an opinion.
        Assert.Null(PeerEstimator.Estimate(Array.Empty<PeerScore>(), minimumPeers: 0));
    }

    [Fact]
    public void TheMedianOfAnOddPeerCountIsTheMiddleScore()
    {
        // The Phoenix 2 quantile with the growth weighting off: five equal voices, so the
        // midpoint convention lands exactly on the third value.
        var peers = new[] { Peer(940_000), Peer(985_000), Peer(962_000), Peer(990_000), Peer(975_000) };

        Assert.Equal(975_000, PeerEstimator.Estimate(peers, growthDecayLevels: 0,
            quantile: PeerEstimator.Phoenix2Quantile, minimumPeers: PeerEstimator.Phoenix2MinimumPeers));
    }

    [Fact]
    public void GrowthWeightingOffMeansEveryScoreIsAFullVoice()
    {
        // The Phoenix 2 configuration: a decay of zero is "off", so a peer who climbed three
        // levels since the score counts exactly like one who did not.
        Assert.Equal(1.0, PeerEstimator.GrowthWeight(3.0, decayLevels: 0), 6);
        Assert.Equal(
            PeerEstimator.Estimate(new[] { new PeerScore(950_000, 22.0, 19.0), Peer(980_000) }, growthDecayLevels: 0),
            PeerEstimator.Estimate(new[] { Peer(950_000), Peer(980_000) }, growthDecayLevels: 0));
    }
}
