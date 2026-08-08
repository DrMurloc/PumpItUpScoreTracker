using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.PlayerProgress.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CohortEstimatorTests
{
    private static PeerScore Peer(int score, double growth = 0) => new(score, 20.0, 20.0 - growth);

    [Fact]
    public void NoPeersMeansNoOpinion()
    {
        Assert.Null(CohortEstimator.Estimate(Array.Empty<PeerScore>()));
    }

    [Fact]
    public void ASinglePeerIsTheEstimateAtEveryQuantile()
    {
        var one = new[] { Peer(950_000) };
        Assert.Equal(950_000, CohortEstimator.Estimate(one, quantile: 0.0));
        Assert.Equal(950_000, CohortEstimator.Estimate(one, quantile: 0.65));
        Assert.Equal(950_000, CohortEstimator.Estimate(one, quantile: 1.0));
    }

    [Fact]
    public void TheEstimateSitsAboveTheMeanOnALeftSkewedCohort()
    {
        // The shape every real chart has: a cluster of good scores plus a tail of
        // barely-passed attempts. A mean lands in the tail; p65 lands in the cluster.
        var peers = new[]
        {
            Peer(600_000), Peer(720_000), Peer(910_000), Peer(945_000), Peer(960_000),
            Peer(965_000), Peer(970_000), Peer(975_000), Peer(980_000), Peer(985_000)
        };
        var mean = peers.Average(p => p.Score);
        var estimate = CohortEstimator.Estimate(peers)!.Value;

        Assert.True(estimate > mean,
            $"expected p65 ({estimate:N0}) above the mean ({mean:N0}) on a left-skewed cohort");
        Assert.True(estimate >= 960_000, $"expected the estimate inside the cluster, got {estimate:N0}");
    }

    [Fact]
    public void RaisingTheQuantileRaisesTheEstimateMonotonically()
    {
        var peers = Enumerable.Range(0, 20).Select(i => Peer(900_000 + i * 5_000)).ToArray();
        var previous = int.MinValue;
        foreach (var q in new[] { 0.1, 0.3, 0.5, 0.65, 0.8, 0.95 })
        {
            var estimate = CohortEstimator.Estimate(peers, quantile: q)!.Value;
            Assert.True(estimate >= previous, $"q={q} produced {estimate:N0} below the previous {previous:N0}");
            previous = estimate;
        }
    }

    [Fact]
    public void APlayerWhoNeverLevelledCountsAtFullVoice()
    {
        Assert.Equal(1.0, CohortEstimator.GrowthWeight(0), 6);
    }

    [Fact]
    public void GrowthWeightFallsAsTheOwnerOutgrowsTheScore()
    {
        var flat = CohortEstimator.GrowthWeight(0);
        var oneLevel = CohortEstimator.GrowthWeight(1);
        var twoLevels = CohortEstimator.GrowthWeight(2);

        Assert.True(oneLevel < flat);
        Assert.True(twoLevels < oneLevel);
        Assert.Equal(Math.Exp(-1), oneLevel, 6);
    }

    [Fact]
    public void ALevelDropDoesNotDiscountAScore()
    {
        // Falling below where you were does not make a past score less representative;
        // only growth does. Guards against a signed subtraction leaking through.
        Assert.Equal(1.0, CohortEstimator.GrowthWeight(-3), 6);
        Assert.Equal(CohortEstimator.Estimate(new[] { new PeerScore(950_000, 18.0, 22.0) }),
            CohortEstimator.Estimate(new[] { new PeerScore(950_000, 22.0, 22.0) }));
    }

    [Fact]
    public void GrownPeersPullTheEstimateTowardTheCurrentOnes()
    {
        // Two populations: stale low scorers and current high scorers. Discounting the
        // stale half must move the estimate up.
        var stale = Enumerable.Range(0, 6).Select(i => new PeerScore(900_000 + i * 1_000, 22.0, 19.0));
        var current = Enumerable.Range(0, 6).Select(i => new PeerScore(970_000 + i * 1_000, 22.0, 22.0));
        var peers = stale.Concat(current).ToArray();

        var weighted = CohortEstimator.Estimate(peers)!.Value;
        var unweighted = CohortEstimator.Estimate(peers, growthDecayLevels: 0)!.Value;

        Assert.True(weighted > unweighted,
            $"growth weighting should favour current scores: {weighted:N0} vs {unweighted:N0}");
    }

    [Fact]
    public void AStableCohortIsUnaffectedByGrowthWeighting()
    {
        // The self-conditioning property: nobody grew, so the weight is inert and the
        // estimate matches the unweighted one exactly.
        var peers = Enumerable.Range(0, 12).Select(i => Peer(930_000 + i * 4_000)).ToArray();

        Assert.Equal(CohortEstimator.Estimate(peers, growthDecayLevels: 0),
            CohortEstimator.Estimate(peers));
    }

    [Fact]
    public void TheWeightingCountsVoicesNotHeads()
    {
        // Three peers, two of them badly outgrown, are worth about one voice between them.
        // The page no longer prints that number, but every quantile Estimate reads is taken
        // over these weights, so the property still has to hold.
        var voices = new[] { 0.0, 3.0, 3.0 }.Sum(g => CohortEstimator.GrowthWeight(g));

        Assert.True(voices < 1.2, $"three peers, two badly outgrown, should be worth ~1 voice; got {voices:N2}");
    }

    [Fact]
    public void TheEstimateNeverLeavesTheObservedRange()
    {
        var peers = new[] { Peer(880_000), Peer(915_000, growth: 2), Peer(1_000_000, growth: 0.5) };

        var estimate = CohortEstimator.Estimate(peers)!.Value;

        Assert.InRange(estimate, 880_000, 1_000_000);
    }
}
