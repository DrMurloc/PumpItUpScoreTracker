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
        IReadOnlyDictionary<Skill, double>? skills = null,
        TierListCategory? passTier = null,
        TierListCategory? scoreTier = null,
        IReadOnlyDictionary<ParagonLevel, double>? letters = null,
        double? scoringLevel = null,
        double? nps = null,
        double? sustain = null,
        double? tension = null,
        double? notes = null,
        string? stepArtist = null,
        SongType songType = SongType.Arcade,
        double? bpm = null,
        MixEnum debut = MixEnum.Phoenix,
        IReadOnlyDictionary<Guid, double>? residuals = null)
    {
        return new ChartSimilarityFeatures(id, Name.From(song), level,
            skills ?? new Dictionary<Skill, double>(), passTier, scoreTier, letters, scoringLevel,
            nps, sustain, tension, notes, stepArtist == null ? (Name?)null : Name.From(stepArtist), songType,
            bpm, debut, residuals ?? new Dictionary<Guid, double>());
    }

    private static IReadOnlyDictionary<Guid, double> Residuals(Func<int, double> valueFor, int count = 30)
    {
        return Enumerable.Range(1, count)
            .ToDictionary(i => new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), valueFor);
    }

    [Fact]
    public void FullSignalPairScoresTheWeightedMeanSymmetrically()
    {
        // Hand-computed: style = 1 (identical vectors); behavior = mean(1, 5/6, 0.85, 0.75)
        // = 0.85833…; players = pearson 1 shrunk by 30/50 = 0.6; intensity = 1 (identical
        // scalars, zero cohort spread); meta = 0.5 + 0.2 + 0.2 + 0.1 = 1. All weights
        // present → total = 0.30 + 0.25·0.858333… + 0.25·0.6 + 0.10 + 0.10 = 0.8645833….
        var a = Features(Guid.NewGuid(), "Song A",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0, [Skill.EndRun] = 0.5 },
            passTier: TierListCategory.Hard, scoreTier: TierListCategory.Medium,
            letters: new Dictionary<ParagonLevel, double> { [ParagonLevel.AA] = 0.2, [ParagonLevel.SSS] = 0.9 },
            scoringLevel: 20.5, nps: 9, sustain: 0.25, tension: 0.7, notes: 800,
            stepArtist: "AEVILUX", bpm: 190, debut: MixEnum.XX,
            residuals: Residuals(i => i));
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0, [Skill.EndRun] = 0.5 },
            passTier: TierListCategory.Hard, scoreTier: TierListCategory.Hard,
            letters: new Dictionary<ParagonLevel, double> { [ParagonLevel.AA] = 0.3, [ParagonLevel.SSS] = 0.7 },
            scoringLevel: 21.0, nps: 9, sustain: 0.25, tension: 0.7, notes: 800,
            stepArtist: "AEVILUX", bpm: 190, debut: MixEnum.XX,
            residuals: Residuals(i => 2 * i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edgeAb = Assert.Single(edges[a.ChartId]);
        var edgeBa = Assert.Single(edges[b.ChartId]);
        Assert.Equal(b.ChartId, edgeAb.SimilarChartId);
        Assert.Equal(a.ChartId, edgeBa.SimilarChartId);
        Assert.Equal(0.8645833333, edgeAb.Score, Tolerance);
        Assert.Equal(edgeAb.Score, edgeBa.Score, Tolerance);
        Assert.Equal(1.0, edgeAb.StyleScore!.Value, Tolerance);
        Assert.Equal(0.8583333333, edgeAb.BehaviorScore!.Value, Tolerance);
        Assert.Equal(0.6, edgeAb.PlayersScore!.Value, Tolerance);
        Assert.Equal(1.0, edgeAb.IntensityScore!.Value, Tolerance);
        Assert.Equal(1.0, edgeAb.MetaScore!.Value, Tolerance);
        Assert.Equal(30, edgeAb.SharedScorers);
    }

    [Fact]
    public void MissingSignalsRenormalizeTheWeightsAndLevelDistanceApplies()
    {
        // Only style (1), pass-tier behavior (1), and meta (0.2 song type + 0.1 debut) are
        // available → (0.30 + 0.25 + 0.03) / 0.65 = 0.8923077, then ×(1 − 0.15) for the
        // one-level gap = 0.7584615….
        var a = Features(Guid.NewGuid(), "Song A", level: 20,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Song B", level: 21,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.7584615385, edge.Score, Tolerance);
        Assert.Null(edge.PlayersScore);
        Assert.Null(edge.IntensityScore);
        Assert.Equal(0.3, edge.MetaScore!.Value, Tolerance);
    }

    [Fact]
    public void FewerThanTwoNonMetaSignalsMakesNoEdge()
    {
        // Style alone (plus metadata) is never enough — metadata must not conjure
        // neighbors out of a single real signal.
        var a = Features(Guid.NewGuid(), "Song A",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            stepArtist: "AEVILUX", bpm: 190);
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            stepArtist: "AEVILUX", bpm: 190);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void NegativeResidualCorrelationClampsToPresentButZero()
    {
        var a = Features(Guid.NewGuid(), "Song A",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i));
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => -i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Equal(0.0, edge.PlayersScore!.Value, Tolerance);
        Assert.Equal(30, edge.SharedScorers);
    }

    [Fact]
    public void FewerThanThirtySharedScorersLeavesThePlayersSignalMissing()
    {
        var a = Features(Guid.NewGuid(), "Song A",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i, 29));
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
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
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(_ => 5.0));
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, residuals: Residuals(i => i));

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        var edge = Assert.Single(edges[a.ChartId]);
        Assert.Null(edge.PlayersScore);
    }

    [Fact]
    public void SameSongChartsAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Same Song",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Same Song", level: 22,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
        Assert.Empty(edges[b.ChartId]);
    }

    [Fact]
    public void LevelsMoreThanTwoApartAreNeverNeighbors()
    {
        var a = Features(Guid.NewGuid(), "Song A", level: 20,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);
        var b = Features(Guid.NewGuid(), "Song B", level: 23,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { a, b });

        Assert.Empty(edges[a.ChartId]);
    }

    [Fact]
    public void LevelAffinityPenalizesDistanceWithinTheWindow()
    {
        // B (same level) and C (two levels up) present identical evidence — same artist,
        // song type, bpm, and debut push meta to 1, so the undamped score is exactly 1.0
        // and C's edge reads the raw 0.70 affinity factor.
        var anchor = Features(Guid.NewGuid(), "Anchor", level: 20,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);
        var sameLevel = Features(Guid.NewGuid(), "Song B", level: 20,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);
        var twoUp = Features(Guid.NewGuid(), "Song C", level: 22,
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, stepArtist: "AEVILUX", bpm: 190);

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor, sameLevel, twoUp });

        var anchorEdges = edges[anchor.ChartId].ToDictionary(e => e.SimilarChartId, e => e.Score);
        Assert.Equal(1.0, anchorEdges[sameLevel.ChartId], Tolerance);
        Assert.Equal(0.7, anchorEdges[twoUp.ChartId], Tolerance);
    }

    [Fact]
    public void EdgesBelowTheFloorAreDropped()
    {
        // style 0.6, pass distance 2 → behavior 2/3, meta 0 (nothing shared) →
        // (0.18 + 0.1666…) / 0.65 = 0.5333… < 0.55.
        var a = Features(Guid.NewGuid(), "Song A",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            passTier: TierListCategory.Medium, songType: SongType.Arcade, debut: MixEnum.Phoenix);
        var b = Features(Guid.NewGuid(), "Song B",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 0.6, [Skill.EndRun] = 0.8 },
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
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 }, scoringLevel: 20.0);
        var neighbors = Enumerable.Range(1, 9).Select(i => Features(Guid.NewGuid(), $"Song {i}",
            skills: new Dictionary<Skill, double> { [Skill.Stamina] = 1.0 },
            scoringLevel: 20.0 + i * 0.05)).ToArray();

        var edges = ChartSimilarityCalculator.BuildEdges(new[] { anchor }.Concat(neighbors).ToArray());

        var anchorEdges = edges[anchor.ChartId];
        Assert.Equal(8, anchorEdges.Count);
        Assert.DoesNotContain(anchorEdges, e => e.SimilarChartId == neighbors[8].ChartId);
        Assert.Equal(anchorEdges.OrderByDescending(e => e.Score).Select(e => e.SimilarChartId),
            anchorEdges.Select(e => e.SimilarChartId));
    }
}
