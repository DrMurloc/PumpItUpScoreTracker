using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartSimilarityCalculatorTests
{
    private const double Tolerance = 1e-6;

    private static ChartSimilarityFeatures Features(
        Guid id,
        string song,
        int level = 20,
        IReadOnlyDictionary<string, double>? badges = null,
        double? nps = null,
        double? sustain = null,
        double? burst = null)
    {
        return new ChartSimilarityFeatures(id, Name.From(song), level,
            badges ?? new Dictionary<string, double>(), nps, sustain, burst);
    }

    [Fact]
    public void BothSignalsRideTheEdgeAndTheScoreIsTheirWeightedGeometricMean()
    {
        // Skill: the shared twist and jack coverage match exactly, so only B's bracket
        // separates them — 1 − 0.25/2.05 = 0.8780488. Intensity: a two-chart cohort puts
        // NPS 9 and 11 a full sigma either side of the mean, so |Δz| = 2 → 1 − 2/3 =
        // 0.3333. The pair asks for the same things and asks very differently hard:
        // 0.8780488^0.75 · 0.3333^0.25 = 0.6892213. An arithmetic 75/25 mean would have
        // read 0.7418699 — skill's three-quarter share nearly buries the intensity gap.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["twist_90"] = 0.9, ["jack"] = 0.3 }, nps: 9);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["twist_90"] = 0.9, ["jack"] = 0.3, ["bracket"] = 0.5 },
            nps: 11);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edgeAb = Assert.Single(edges[a.ChartId]);
        var edgeBa = Assert.Single(edges[b.ChartId]);
        Assert.Equal(b.ChartId, edgeAb.SimilarChartId);
        Assert.Equal(a.ChartId, edgeBa.SimilarChartId);
        Assert.Equal(0.6892212663, edgeAb.Score, Tolerance);
        Assert.Equal(edgeAb.Score, edgeBa.Score, Tolerance);
        Assert.Equal(0.8780487805, edgeAb.SkillScore, Tolerance);
        Assert.Equal(0.3333333333, edgeAb.IntensityScore, Tolerance);
    }

    [Fact]
    public void AChartWithoutStepAnalysisGetsNoEdges()
    {
        // Both signals are mandatory. Badges and step analysis come from the same crawl,
        // so half-evidence means the chart is half-known, not that it has no neighbors.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 });
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 });

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void AChartWithoutBankedBadgesGetsNoEdges()
    {
        var a = Features(Guid.NewGuid(), "Song A", nps: 10);
        var b = Features(Guid.NewGuid(), "Song B", nps: 10);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void SameSongChartsAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Same Song",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var b = Features(Guid.NewGuid(), "Same Song", level: 22,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void LevelsMoreThanTwoApartAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Song A", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var b = Features(Guid.NewGuid(), "Song B", level: 23,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
    }

    [Fact]
    public void LevelDistanceInsideTheWindowCostsNothing()
    {
        // B (same level) and C (two folders up) present identical evidence. The folder
        // level is Andamiro's passing level, inconsistently applied, so it earns no
        // penalty of its own: the window limits reach, and how hard the chart will be for
        // the viewer is the shelf's ordering, not the score's business. Both edges read 1.0.
        var anchor = Features(Guid.NewGuid(), "Anchor", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var sameLevel = Features(Guid.NewGuid(), "Song B", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var twoUp = Features(Guid.NewGuid(), "Song C", level: 22,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor, sameLevel, twoUp });

        var anchorEdges = edges[anchor.ChartId].ToDictionary(e => e.SimilarChartId, e => e.Score);
        Assert.Equal(1.0, anchorEdges[sameLevel.ChartId], Tolerance);
        Assert.Equal(1.0, anchorEdges[twoUp.ChartId], Tolerance);
    }

    [Fact]
    public void APoorPairIsStoredRatherThanDropped()
    {
        // Gamma-shaped 1.0 vs (0.36, 0.64): skill = 1 − 1.28/2.0 = 0.36, intensity a
        // perfect 1.0, so the pair scores 0.36^0.75 = 0.4647580 — under any floor the
        // shelf is likely to set. It is kept anyway. Nothing but the gates removes a
        // pair: this row is what the near-misses section renders, and it is why the bar
        // can move without a rebuild.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 0.6, ["run"] = 0.8 }, nps: 10);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.4647580015, edge.Score, Tolerance);
    }

    [Fact]
    public void OnlyTheTopTwentyNeighborsSurvive()
    {
        // Twenty-one candidates, scores strictly decreasing via a bracket-coverage
        // gradient — the twenty-first is the one that must fall off. Every NPS is
        // identical, so intensity is 1.0 throughout and skill alone does the ordering.
        var anchor = Features(Guid.NewGuid(), "Anchor",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 10);
        var neighbors = Enumerable.Range(1, 21).Select(i => Features(Guid.NewGuid(), $"Song {i}",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 - i * 0.01 }, nps: 10)).ToArray();

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor }.Concat(neighbors).ToArray());

        var anchorEdges = edges[anchor.ChartId];
        Assert.Equal(20, anchorEdges.Count);
        Assert.DoesNotContain(anchorEdges, e => e.SimilarChartId == neighbors[20].ChartId);
        Assert.Equal(anchorEdges.OrderByDescending(e => e.Score).Select(e => e.SimilarChartId),
            anchorEdges.Select(e => e.SimilarChartId));
    }

    [Fact]
    public void TheSharedBadgesAreTheOverlapBestFirstAndReadAsRawCoverage()
    {
        // What the two charts have in common is min(a, b) per badge, on raw coverage: the
        // pair shares half its brackets and a quarter of its anchor runs. Reported
        // un-gamma'd, because "bracket 0.50" has to mean both charts really are at least
        // half brackets — squaring is for the distance, not for the human. drill rides A
        // alone and jack rides B alone, so neither is shared at any coverage.
        var a = Features(Guid.NewGuid(), "Song A", nps: 10,
            badges: new Dictionary<string, double>
                { ["bracket"] = 0.50, ["anchor_run"] = 0.90, ["drill"] = 0.40 });
        var b = Features(Guid.NewGuid(), "Song B", nps: 10,
            badges: new Dictionary<string, double>
                { ["bracket"] = 0.80, ["anchor_run"] = 0.25, ["jack"] = 0.60 });

        var edge = Assert.Single(ChartSimilarityCalculator.BuildEdges(new[] { a, b })[a.ChartId]);

        Assert.Equal(new[] { "bracket", "anchor_run" }, edge.SharedBadges.Select(s => s.Badge).ToArray());
        Assert.Equal(0.50, edge.SharedBadges[0].Coverage, Tolerance);
        Assert.Equal(0.25, edge.SharedBadges[1].Coverage, Tolerance);
    }

    [Fact]
    public void OnlyTheTopFiveSharedBadgesRideTheEdge()
    {
        var badges = new Dictionary<string, double>
        {
            ["bracket"] = 0.9, ["anchor_run"] = 0.8, ["drill"] = 0.7, ["jack"] = 0.6,
            ["jump"] = 0.5, ["run"] = 0.4, ["twist_90"] = 0.3
        };
        var a = Features(Guid.NewGuid(), "Song A", badges: badges, nps: 10);
        var b = Features(Guid.NewGuid(), "Song B", badges: badges, nps: 10);

        var edge = Assert.Single(ChartSimilarityCalculator.BuildEdges(new[] { a, b })[a.ChartId]);

        Assert.Equal(new[] { "bracket", "anchor_run", "drill", "jack", "jump" },
            edge.SharedBadges.Select(s => s.Badge).ToArray());
    }

    [Fact]
    public void AMatchingNpsAndSustainCannotRescueAMismatchedBurstProfile()
    {
        // Eight charts cluster at a 0.10 burst fraction; the ninth spends half its length
        // bursting. Every NPS and every sustain fraction is identical, so those two
        // dimensions read a perfect 1.0 and only burst separates the outlier — at 3.18
        // cohort sigma, which clamps its similarity to zero. The geometric mean returns
        // 0.1584893, where a flat 20/40/40 arithmetic mean over the very same dimensions
        // reads 0.60 and would ship the pair. This is the real D21 shape: Slapstick
        // Parfait and Horang Pungryuga run at the same 10.7 NPS over .109-vs-.095
        // sustain and .118-vs-.516 burst, and measured 0.1558 against arithmetic's 0.5831.
        var badges = new Dictionary<string, double> { ["bracket"] = 1.0 };
        var cluster = Enumerable.Range(1, 8).Select(i => Features(Guid.NewGuid(), $"Song {i}", level: 21,
            badges: badges, nps: 10, sustain: 0.10, burst: 0.10)).ToArray();
        var outlier = Features(Guid.NewGuid(), "Horang-like", level: 21,
            badges: badges, nps: 10, sustain: 0.10, burst: 0.50);

        var edges = ChartSimilarityCalculator.BuildEdges(cluster.Append(outlier).ToArray());

        var anchor = edges[cluster[0].ChartId];
        var toOutlier = Assert.Single(anchor, e => e.SimilarChartId == outlier.ChartId);
        Assert.Equal(0.1584893192, toOutlier.IntensityScore, Tolerance);
        Assert.Equal(0.6309573445, toOutlier.Score, Tolerance);
        // Its clustered siblings are identical on all three dimensions.
        Assert.All(anchor.Where(e => e.SimilarChartId != outlier.ChartId),
            e => Assert.Equal(1.0, e.IntensityScore, Tolerance));
    }

    [Fact]
    public void IntensityRenormalizesOverTheDimensionsThePairActuallyHas()
    {
        // A song with no duration banked has no sustain or burst fraction, so NPS carries
        // the signal alone and takes the full weight rather than two-tenths of it: |Δz| = 2
        // → 1 − 2/3 = 0.3333, not 0.3333 diluted by two absent dimensions.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 8);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 12);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.3333333333, edge.IntensityScore, Tolerance);
    }

    [Fact]
    public void IntensityZScoresEachScalarWithinTheLevelCohort()
    {
        // NPS 8 and 12 → cohort mean 10, std 2 → z −1 and +1 → |Δz| = 2 → 1 − 2/3 =
        // 0.3333…. Sustain and tension are absent, so NPS is the only live dimension.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 8);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, nps: 12);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.3333333333, edge.IntensityScore, Tolerance);
        Assert.Equal(0.7598356857, edge.Score, Tolerance);
    }

    // The skill metric is read directly: routing these through BuildEdges would couple a
    // metric fixture to the floor, and the whole point of the geometric mean is that a low
    // skill score sinks the edge before it can be asserted on.
    private static double Skill(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        return ChartSimilarityCalculator.SkillSimilarity(
            Features(Guid.NewGuid(), "Song A", badges: a),
            Features(Guid.NewGuid(), "Song B", badges: b))!.Score;
    }

    [Fact]
    public void ATraceOfABadgeIsFarFromAChartBuiltOnIt()
    {
        // Gamma-shaped: 0.10 → 0.01 against 0.95 → 0.9025, so the trace contributes almost
        // no mass and the verdict is the chart that is actually built on brackets:
        // 1 − 0.8925/0.9125 = 0.0219…. An angle-only metric would call these identical
        // (both vectors point along bracket); a mean over the union would report 0.15
        // regardless of how much other coverage the charts shared.
        Assert.Equal(0.0219178082,
            Skill(new Dictionary<string, double> { ["bracket"] = 0.10 },
                new Dictionary<string, double> { ["bracket"] = 0.95 }), Tolerance);
    }

    [Fact]
    public void ABadgeOneChartNeverCarriesCostsItsWholeMagnitude()
    {
        // Union, not intersection: a badge absent from A is A carrying none of it, not
        // missing data. The shared twist coverage lands in the mass and argues for the
        // pair; the unmatched bracket lands in the difference. 1 − 0.81/2.43 = 0.6667….
        Assert.Equal(0.6666666667,
            Skill(new Dictionary<string, double> { ["twist_90"] = 0.9 },
                new Dictionary<string, double> { ["twist_90"] = 0.9, ["bracket"] = 0.9 }), Tolerance);
    }

    [Fact]
    public void SharedBaselineCoverageBarelyArguesForAPair()
    {
        // Both charts carry the corpus baseline (~0.25 on the common badges) at identical
        // coverage; only bracket differs. Gamma is what keeps that baseline from voting:
        // each 0.25 badge drops to 0.0625, so the tail contributes 0.5 of mass instead of
        // 2.0 and stops drowning the one real gap. 1 − 0.64/(0.64 + 4·0.125) = 0.4386…
        // (at γ=1 the same charts read 0.7143).
        var tail = new Dictionary<string, double>
            { ["jump"] = 0.25, ["jack"] = 0.25, ["run"] = 0.25, ["drill"] = 0.25 };

        Assert.Equal(0.4385964912,
            Skill(new Dictionary<string, double>(tail) { ["bracket"] = 0.8 },
                new Dictionary<string, double>(tail)), Tolerance);
    }

    [Fact]
    public void AgreeingOnTheBaselineLosesToDifferingOnWhatDefinesEachChart()
    {
        // Both charts jack at the baseline; one is a bracket chart, the other a twist
        // chart. Squaring turns the 0.9-vs-0.3 influence ratio from 7:1 into 81:9, so the
        // shared baseline cannot argue the pair alike: 1 − 1.62/1.80 = 0.10 (at γ=1 the
        // same charts read 0.25).
        Assert.Equal(0.1,
            Skill(new Dictionary<string, double> { ["jack"] = 0.3, ["bracket"] = 0.9 },
                new Dictionary<string, double> { ["jack"] = 0.3, ["twist_90"] = 0.9 }), Tolerance);
    }
}
