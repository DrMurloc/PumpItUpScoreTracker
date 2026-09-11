using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
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
    private static IReadOnlySet<PeerVoice> Voices(params Guid[] ids) =>
        ids.Select(PeerVoice.Account).ToHashSet();

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

        var summary = PumbilityPeerPools.Build(records, Voices(peer), catalog, Scoring);

        Assert.Equal(50, summary.Pools[PeerVoice.Account(peer)].Count);
        Assert.Equal(50, summary.Charts[expected[0]].Points);
        Assert.Equal(1, summary.Charts[expected[49]].Points);
        Assert.Equal(expected.Take(50).ToHashSet(), summary.Pools[PeerVoice.Account(peer)]);
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

        var summary = PumbilityPeerPools.Build(records, Voices(a, b), catalog, Scoring);

        // A's pool: shared (#1, 50) then onlyA (#2, 49). B's: onlyB (#1, 50) then shared (#2, 49).
        Assert.Equal(2, summary.Charts[shared.Id].Holders);
        Assert.Equal(99, summary.Charts[shared.Id].Points);
        Assert.Equal(1, summary.Charts[onlyA.Id].Holders);
        Assert.Equal(49, summary.Charts[onlyA.Id].Points);
        Assert.Equal(50, summary.Charts[onlyB.Id].Points);
        Assert.Equal(new[] { a, b }.Select(PeerVoice.Account).ToHashSet(), summary.Peers);
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

        var summary = PumbilityPeerPools.Build(records, Voices(peer),
            new Dictionary<Guid, Chart> { [chart.Id] = chart }, Scoring);

        Assert.Equal(1, summary.Charts[chart.Id].Holders);
        Assert.Equal(1, summary.Charts[chart.Id].Scored);
        Assert.Single(summary.Charts);
        Assert.False(summary.Pools.ContainsKey(PeerVoice.Account(stranger)));
    }

    [Fact]
    public void AProjectedGradeNeedsFiveScoresAndReadsEveryScorerNotJustHolders()
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

        var summary = PumbilityPeerPools.Build(records, Voices(peers), catalog, Scoring);

        var entry = summary.Charts[chart.Id];
        Assert.Equal(4, entry.Holders);
        Assert.Equal(5, entry.Scored);
        Assert.False(summary.Pools[PeerVoice.Account(peers[4])].Contains(chart.Id));
        // Midpoint-convention quantiles over 940 / 962 / 975 / 985 / 990k — the estimator's own
        // arithmetic, read on demand at any rung (D51, D52).
        Assert.Equal(975_000, (int)entry.ProjectedAt(PeerEstimator.Median)!.Value);
        Assert.Equal(956_500, (int)entry.ProjectedAt(PeerEstimator.LowerQuartile)!.Value);
        Assert.Equal(986_250, (int)entry.ProjectedAt(PeerEstimator.UpperQuartile)!.Value);

        // Four scorers: held, so it appears — but no opinion at any rung.
        var four = PumbilityPeerPools.Build(records.Where(r => r.UserId != peers[4]).ToArray(),
            Voices(peers.Take(4).ToArray()), catalog, Scoring);
        Assert.Equal(4, four.Charts[chart.Id].Scored);
        Assert.Null(four.Charts[chart.Id].ProjectedAt(PeerEstimator.Median));
        Assert.Null(four.Charts[chart.Id].ProjectedAt(PeerEstimator.LowerQuartile));
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

        var five = PumbilityPeerPools.Build(records, Voices(peers), catalog, Scoring);
        Assert.Equal(0, five.Charts[low.Id].Holders);
        Assert.Equal(5, five.Charts[low.Id].Scored);
        Assert.NotNull(five.Charts[low.Id].ProjectedAt(PeerEstimator.Median));

        var four = PumbilityPeerPools.Build(records.Take(4).ToArray(), Voices(peers.Take(4).ToArray()), catalog, Scoring);
        Assert.False(four.Charts.ContainsKey(low.Id));
    }

    [Fact]
    public void ALevelIsInReachWhenAtLeastHalfThePeersHoldingAnythingKeepAChartOfIt()
    {
        var s20 = Chart(20);
        var s21 = Chart(21);
        var otherS21 = Chart(21);
        var s23 = Chart(23);
        var catalog = new[] { s20, s21, otherS21, s23 }.ToDictionary(c => c.Id);
        var peers = Enumerable.Range(0, 4).Select(_ => PeerVoice.Account(Guid.NewGuid())).ToArray();

        // Every peer keeps an S20. Two keep an S21, different ones: exactly half, and it is the
        // level that counts. One keeps an S23, a quarter. A chart outside the catalog has no level.
        var summary = Summary(peers,
            new[] { s20.Id, s21.Id, s23.Id, Guid.NewGuid() },
            new[] { s20.Id, otherS21.Id },
            new[] { s20.Id },
            new[] { s20.Id });

        Assert.Equal(new[] { 20, 21 }, PumbilityPeerPools.LevelsInReach(summary, catalog).ToArray());
    }

    [Fact]
    public void APeerWhosePoolIsEmptyCountsAgainstNoLevel()
    {
        var s22 = Chart(22);
        var catalog = new Dictionary<Guid, Chart> { [s22.Id] = s22 };
        var peers = Enumerable.Range(0, 3).Select(_ => PeerVoice.Account(Guid.NewGuid())).ToArray();

        // One peer keeps an S22 and two hold nothing: one of the one peer holding anything.
        var summary = Summary(peers, new[] { s22.Id }, Array.Empty<Guid>(), Array.Empty<Guid>());

        Assert.Equal(new[] { 22 }, PumbilityPeerPools.LevelsInReach(summary, catalog).ToArray());
    }

    [Fact]
    public void ABoardPeerCountsLikeAnAccountAndNoPoolsMeanNoLevels()
    {
        var s20 = Chart(20);
        var s24 = Chart(24);
        var catalog = new[] { s20, s24 }.ToDictionary(c => c.Id);
        var peers = new[] { PeerVoice.Account(Guid.NewGuid()), PeerVoice.FromBoard(7, "BOARD#1234") };

        var summary = Summary(peers, new[] { s20.Id }, new[] { s24.Id });

        Assert.Equal(new[] { 20, 24 }, PumbilityPeerPools.LevelsInReach(summary, catalog).ToArray());
        Assert.Empty(PumbilityPeerPools.LevelsInReach(Summary(Array.Empty<PeerVoice>()), catalog));
    }

    /// <summary>A summary holding exactly these pools, one per peer in order, and no chart statistics.</summary>
    private static PeerPoolSummary Summary(PeerVoice[] peers, params Guid[][] pools)
    {
        return new PeerPoolSummary(peers.ToHashSet(),
            peers.Select((peer, i) => (peer, pool: (IReadOnlySet<Guid>)pools[i].ToHashSet()))
                .ToDictionary(p => p.peer, p => p.pool),
            new Dictionary<Guid, PeerPoolChart>());
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
