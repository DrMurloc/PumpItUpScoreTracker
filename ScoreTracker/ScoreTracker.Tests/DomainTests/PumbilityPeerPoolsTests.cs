using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The prevalence arithmetic (docs/design/pumbility-overhaul.md D33): a peer's #1 chart is
///     worth 50 and their #50 is worth 1, holders are counted, and the score statistics read the
///     peers who scored a chart with the projection's own floor.
/// </summary>
public sealed class PumbilityPeerPoolsTests
{
    private static readonly ScoringConfiguration Scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

    [Fact]
    public void AChartAtTheTopOfAPoolScoresFiftyAndAtTheBottomScoresOne()
    {
        // One peer, fifty-two priced charts spread over levels and grades. The pool is the fifty
        // highest-priced, ties broken by chart id as the builder breaks them.
        var charts = Enumerable.Range(0, 52).Select(i => Chart(24 - i / 4)).ToArray();
        var peer = Guid.NewGuid();
        var records = charts.Select((c, i) => Score(peer, c.Id, 990_000 - i * 1_000)).ToArray();
        var catalog = charts.ToDictionary(c => c.Id);
        var expected = records
            .Select(r => (r.ChartId, Rating: Scoring.GetScore(catalog[r.ChartId], r.Score, PhoenixPlate.MarvelousGame, false)))
            .OrderByDescending(r => r.Rating).ThenBy(r => r.ChartId)
            .Select(r => r.ChartId).ToArray();

        var summary = PumbilityPeerPools.Build(records, new HashSet<Guid> { peer }, catalog, Scoring);

        Assert.Equal(50, summary.Pools[peer].Count);
        Assert.Equal(50, summary.Charts[expected[0]].Points);
        Assert.Equal(1, summary.Charts[expected[49]].Points);
        Assert.Equal(expected.Take(50).ToHashSet(), summary.Pools[peer]);
        // The two that fell outside the fifty are scored, not held — and one scorer earns no row.
        Assert.False(summary.Charts.ContainsKey(expected[50]));
        Assert.False(summary.Charts.ContainsKey(expected[51]));
        Assert.Equal(1275, summary.Charts.Values.Sum(c => c.Points));
    }

    [Fact]
    public void PointsAndHoldersSumAcrossPeersAndEveryPeerCastsTheSameVote()
    {
        var shared = Chart(22);
        var onlyA = Chart(21);
        var onlyB = Chart(23);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var records = new[]
        {
            Score(a, shared.Id, 990_000), Score(a, onlyA.Id, 980_000),
            Score(b, onlyB.Id, 995_000), Score(b, shared.Id, 970_000)
        };
        var catalog = new[] { shared, onlyA, onlyB }.ToDictionary(c => c.Id);

        var summary = PumbilityPeerPools.Build(records, new HashSet<Guid> { a, b }, catalog, Scoring);

        // A's pool: shared (#1, 50) then onlyA (#2, 49). B's: onlyB (#1, 50) then shared (#2, 49).
        Assert.Equal(2, summary.Charts[shared.Id].Holders);
        Assert.Equal(99, summary.Charts[shared.Id].Points);
        Assert.Equal(1, summary.Charts[onlyA.Id].Holders);
        Assert.Equal(49, summary.Charts[onlyA.Id].Points);
        Assert.Equal(50, summary.Charts[onlyB.Id].Points);
        Assert.Equal(new[] { a, b }.ToHashSet(), summary.PeerIds);
    }

    [Fact]
    public void RecordsOfNonPeersAndUnknownChartsAreIgnored()
    {
        var chart = Chart(20);
        var peer = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var records = new[]
        {
            Score(peer, chart.Id, 990_000),
            Score(stranger, chart.Id, 999_000),
            Score(peer, Guid.NewGuid(), 999_000)
        };

        var summary = PumbilityPeerPools.Build(records, new HashSet<Guid> { peer },
            new Dictionary<Guid, Chart> { [chart.Id] = chart }, Scoring);

        Assert.Equal(1, summary.Charts[chart.Id].Holders);
        Assert.Equal(1, summary.Charts[chart.Id].Scored);
        Assert.Single(summary.Charts);
        Assert.False(summary.Pools.ContainsKey(stranger));
    }

    [Fact]
    public void TheMedianAndQuartilesNeedFiveScoresAndReadEveryScorerNotJustHolders()
    {
        var chart = Chart(22);
        var fillers = Enumerable.Range(0, 50).Select(_ => Chart(24)).ToArray();
        var catalog = fillers.Append(chart).ToDictionary(c => c.Id);
        var scores = new[] { 940_000, 985_000, 962_000, 990_000, 975_000 };
        var peers = scores.Select(_ => Guid.NewGuid()).ToArray();
        // Four peers hold it. The fifth scored it too, but fifty stronger charts fill their pool.
        var records = peers.SelectMany((p, i) => i < 4
            ? new[] { Score(p, chart.Id, scores[i]) }
            : fillers.Select(f => Score(p, f.Id, 999_000)).Append(Score(p, chart.Id, scores[i]))).ToArray();

        var summary = PumbilityPeerPools.Build(records, peers.ToHashSet(), catalog, Scoring);

        var entry = summary.Charts[chart.Id];
        Assert.Equal(4, entry.Holders);
        Assert.Equal(5, entry.Scored);
        // Midpoint-convention quantiles over 940 / 962 / 975 / 985 / 990k — the estimator's own.
        Assert.Equal(975_000, (int)entry.Median!.Value);
        Assert.Equal(956_500, (int)entry.Quartile1!.Value);
        Assert.Equal(986_250, (int)entry.Quartile3!.Value);
        Assert.False(summary.Pools[peers[4]].Contains(chart.Id));
        // A rung read on demand is the same arithmetic over the same voices (D51, D52).
        Assert.Equal(975_000, (int)entry.ProjectedAt(PeerEstimator.Median)!.Value);
        Assert.Equal(956_500, (int)entry.ProjectedAt(PeerEstimator.DefaultQuantile)!.Value);
        Assert.Equal(986_250, (int)entry.ProjectedAt(PeerEstimator.UpperQuartile)!.Value);

        // Four scorers: held, so it appears — but no median.
        var four = PumbilityPeerPools.Build(records.Where(r => r.UserId != peers[4]).ToArray(),
            peers.Take(4).ToHashSet(), catalog, Scoring);
        Assert.Equal(4, four.Charts[chart.Id].Scored);
        Assert.Null(four.Charts[chart.Id].Median);
        Assert.Null(four.Charts[chart.Id].Quartile1);
        Assert.Null(four.Charts[chart.Id].ProjectedAt(PeerEstimator.Median));
    }

    [Fact]
    public void AChartNobodyHoldsAppearsOnlyOnceFiveHaveScoredIt()
    {
        // A level-9 chart prices at zero on Phoenix 2 and can hold no pool slot, so it is a chart
        // the peers scored and nobody holds: it earns a row only once five of them have.
        var low = Chart(9);
        var catalog = new Dictionary<Guid, Chart> { [low.Id] = low };
        var peers = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var records = peers.Select(p => Score(p, low.Id, 990_000)).ToArray();

        var five = PumbilityPeerPools.Build(records, peers.ToHashSet(), catalog, Scoring);
        Assert.Equal(0, five.Charts[low.Id].Holders);
        Assert.Equal(5, five.Charts[low.Id].Scored);
        Assert.NotNull(five.Charts[low.Id].Median);

        var four = PumbilityPeerPools.Build(records.Take(4).ToArray(), peers.Take(4).ToHashSet(), catalog, Scoring);
        Assert.False(four.Charts.ContainsKey(low.Id));
    }

    private static Chart Chart(int level)
    {
        return new ChartBuilder().WithId(Guid.NewGuid()).WithMix(MixEnum.Phoenix2).WithType(ChartType.Single)
            .WithLevel(level).Build();
    }

    private static UserPhoenixScore Score(Guid user, Guid chart, int score)
    {
        return new UserPhoenixScore(user, chart, "Peer", score, PhoenixPlate.MarvelousGame, false);
    }
}
