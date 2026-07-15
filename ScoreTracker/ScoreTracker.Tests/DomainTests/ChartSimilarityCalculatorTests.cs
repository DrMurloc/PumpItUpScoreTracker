using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
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
        TierListCategory? passTier = null,
        TierListCategory? scoreTier = null,
        IReadOnlyDictionary<ParagonLevel, double>? letters = null,
        double? scoringLevel = null,
        double? nps = null,
        double? sustain = null,
        double? tension = null,
        string? stepArtist = null,
        SongType songType = SongType.Arcade,
        double? bpm = null,
        MixEnum debut = MixEnum.Phoenix,
        IReadOnlyDictionary<Guid, double>? residuals = null)
    {
        return new ChartSimilarityFeatures(id, Name.From(song), level,
            badges ?? new Dictionary<string, double>(), passTier, scoreTier, letters, scoringLevel,
            nps, sustain, tension, stepArtist == null ? (Name?)null : Name.From(stepArtist), songType,
            bpm, debut, residuals ?? new Dictionary<Guid, double>());
    }

    private static IReadOnlyDictionary<Guid, double> Residuals(Func<int, double> valueFor, int count = 30)
    {
        return Enumerable.Range(1, count)
            .ToDictionary(i => new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), valueFor);
    }

    [Fact]
    public void FullSignalPairScoresTheWeightedGeometricMeanSymmetrically()
    {
        // Hand-computed: skill = 1 (identical vectors, zero coverage distance); difficulty
        // = mean(1, 5/6, 0.85, 0.75) = 0.85833…; players = pearson 1 shrunk by 30/50 = 0.6;
        // intensity = 1 (identical scalars, zero cohort spread); meta = 0.5 + 0.2 + 0.2 +
        // 0.1 = 1. All weights present, so the geometric mean is
        // exp(0.25·ln(0.858333…) + 0.25·ln(0.6)) = exp(−0.1658970) = 0.8471334…. An
        // arithmetic mean would have read 0.8645833 — the 0.6 costs far more in log space.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0, ["run"] = 0.5 },
            passTier: TierListCategory.Hard, scoreTier: TierListCategory.Medium,
            letters: new Dictionary<ParagonLevel, double> { [ParagonLevel.AA] = 0.2, [ParagonLevel.SSS] = 0.9 },
            scoringLevel: 20.5, nps: 9, sustain: 0.25, tension: 0.7,
            stepArtist: "AEVILUX", bpm: 190, debut: MixEnum.XX,
            residuals: Residuals(i => i));
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0, ["run"] = 0.5 },
            passTier: TierListCategory.Hard, scoreTier: TierListCategory.Hard,
            letters: new Dictionary<ParagonLevel, double> { [ParagonLevel.AA] = 0.3, [ParagonLevel.SSS] = 0.7 },
            scoringLevel: 21.0, nps: 9, sustain: 0.25, tension: 0.7,
            stepArtist: "AEVILUX", bpm: 190, debut: MixEnum.XX,
            residuals: Residuals(i => 2 * i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edgeAb = Assert.Single(edges[a.ChartId]);
        var edgeBa = Assert.Single(edges[b.ChartId]);
        Assert.Equal(b.ChartId, edgeAb.SimilarChartId);
        Assert.Equal(a.ChartId, edgeBa.SimilarChartId);
        Assert.Equal(0.8471334043, edgeAb.Score, Tolerance);
        Assert.Equal(edgeAb.Score, edgeBa.Score, Tolerance);
        Assert.Equal(1.0, edgeAb.SkillScore!.Value, Tolerance);
        Assert.Equal(0.8583333333, edgeAb.DifficultyScore!.Value, Tolerance);
        Assert.Equal(0.6, edgeAb.PlayersScore!.Value, Tolerance);
        Assert.Equal(1.0, edgeAb.IntensityScore!.Value, Tolerance);
        Assert.Equal(1.0, edgeAb.MetaScore!.Value, Tolerance);
        Assert.Equal(30, edgeAb.SharedScorers);
    }

    [Fact]
    public void MissingSignalsRenormalizeTheWeights()
    {
        // Only skill (1), pass-tier difficulty (1), and meta (0.2 song type + 0.1 debut)
        // are available → exp(0.10·ln(0.3) / 0.65) = 0.8309160. The one-folder gap costs
        // nothing.
        var a = Features(Guid.NewGuid(), "Song A", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Song B", level: 21,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.8309159892, edge.Score, Tolerance);
        Assert.Null(edge.PlayersScore);
        Assert.Null(edge.IntensityScore);
        Assert.Equal(0.3, edge.MetaScore!.Value, Tolerance);
    }

    [Fact]
    public void FewerThanTwoNonMetaSignalsMakesNoEdge()
    {
        // Skill alone (plus metadata) is never enough — metadata must not conjure
        // neighbors out of a single real signal.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            stepArtist: "AEVILUX", bpm: 190);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            stepArtist: "AEVILUX", bpm: 190);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void ActivelyDissimilarPlayersAreNeverANeighbour()
    {
        // Negative correlation clamps to zero, and zero is unsurvivable in the geometric
        // mean: worth SignalFloor at the players weight, exp(0.25·ln(0.01)) caps the score
        // at 0.316 even with every other signal perfect. The shelf never needs opposites,
        // and no amount of agreement elsewhere can vote that down.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i));
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => -i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void FewerThanThirtySharedScorersLeavesThePlayersSignalMissing()
    {
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i, 29));
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => 2 * i, 29));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Null(edge.PlayersScore);
        Assert.Equal(29, edge.SharedScorers);
    }

    [Fact]
    public void ZeroVarianceResidualsLeaveThePlayersSignalMissing()
    {
        // A constant residual set has no correlation to measure — undefined, not zero.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(_ => 5.0));
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Null(edge.PlayersScore);
    }

    [Fact]
    public void SameSongChartsAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Same Song",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Same Song", level: 22,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void LevelsMoreThanTwoApartAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Song A", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Song B", level: 23,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
    }

    [Fact]
    public void LevelDistanceInsideTheWindowCostsNothing()
    {
        // B (same level) and C (two folders up) present identical evidence. The folder
        // level is Andamiro's passing level, inconsistently applied, so it earns no
        // penalty of its own: the window limits reach, and what a chart demands and how
        // hard it really is are Skill's and Difficulty's jobs. Both edges read 1.0.
        var anchor = Features(Guid.NewGuid(), "Anchor", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);
        var sameLevel = Features(Guid.NewGuid(), "Song B", level: 20,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);
        var twoUp = Features(Guid.NewGuid(), "Song C", level: 22,
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor, sameLevel, twoUp });

        var anchorEdges = edges[anchor.ChartId].ToDictionary(e => e.SimilarChartId, e => e.Score);
        Assert.Equal(1.0, anchorEdges[sameLevel.ChartId], Tolerance);
        Assert.Equal(1.0, anchorEdges[twoUp.ChartId], Tolerance);
    }

    [Fact]
    public void EdgesBelowTheFloorAreDropped()
    {
        // Gamma-shaped 1.0 vs (0.36, 0.64): skill = 1 − 1.28/2.0 = 0.36; pass distance 2 →
        // difficulty 2/3; meta 0, which is worth SignalFloor → exp((0.30·ln(0.36) +
        // 0.25·ln(0.6667) + 0.10·ln(0.01)) / 0.65) = 0.2629 < 0.55.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, songType: SongType.Arcade, debut: MixEnum.Phoenix);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 0.6, ["run"] = 0.8 },
            passTier: TierListCategory.VeryHard, songType: SongType.Remix, debut: MixEnum.XX);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
    }

    [Fact]
    public void OnlyTheTopEightNeighborsSurvive()
    {
        // Nine candidates, scores strictly decreasing via a scoring-level gradient —
        // the ninth-best is the one that must fall off.
        var anchor = Features(Guid.NewGuid(), "Anchor",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 }, scoringLevel: 20.0);
        var neighbors = Enumerable.Range(1, 9).Select(i => Features(Guid.NewGuid(), $"Song {i}",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            scoringLevel: 20.0 + i * 0.05)).ToArray();

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor }.Concat(neighbors).ToArray());

        var anchorEdges = edges[anchor.ChartId];
        Assert.Equal(8, anchorEdges.Count);
        Assert.DoesNotContain(anchorEdges, e => e.SimilarChartId == neighbors[8].ChartId);
        Assert.Equal(anchorEdges.OrderByDescending(e => e.Score).Select(e => e.SimilarChartId),
            anchorEdges.Select(e => e.SimilarChartId));
    }

    // The skill metric is read directly: routing these through BuildEdges would couple a
    // metric fixture to the floor, and the whole point of the geometric mean is that a low
    // skill score sinks the edge before it can be asserted on.
    private static double Skill(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        return ChartSimilarityCalculator.SkillSimilarity(
            Features(Guid.NewGuid(), "Song A", badges: a),
            Features(Guid.NewGuid(), "Song B", badges: b))!.Value;
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

    [Fact]
    public void IntensityZScoresEachScalarWithinTheLevelCohort()
    {
        // NPS 8 and 12 → cohort mean 10, std 2 → z −1 and +1 → |Δz| = 2 → 1 − 2/3 =
        // 0.3333…. Sustain and tension are absent, so NPS is the only live dimension.
        var a = Features(Guid.NewGuid(), "Song A",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, nps: 8);
        var b = Features(Guid.NewGuid(), "Song B",
            badges: new Dictionary<string, double> { ["bracket"] = 1.0 },
            passTier: TierListCategory.Medium, nps: 12);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.3333333333, edge.IntensityScore!.Value, Tolerance);
    }
}
