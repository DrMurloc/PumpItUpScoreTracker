using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The projector's plumbing, mix by mix: who it asks for, what it reads, and what it refuses
///     to read. Phoenix 2 draws PUMBILITY peers — the window on the pool of the type
///     (docs/design/pumbility-overhaul.md §4.8, §4.11, D53) — and never touches Phoenix 1; Phoenix 1
///     draws a competitive band and never touches Phoenix 2.
///     Both fill the peers' pools when handed the catalog (§3.10, D43). The arithmetic itself is
///     PeerEstimatorTests' business.
/// </summary>
public sealed class ScoreProjectorTests
{
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    // ------------------------------------------------------------------ Phoenix 2

    [Fact]
    public async Task Phoenix2PeersAreTheWindowOnThePoolOfTheType()
    {
        // A singles pool of 17,600 reaches 500 down and 250 up (D53): the range asked of the stats
        // reader, inclusive both ends, on the singles pool — never on the merged total.
        var ctx = new Context(viewerPool: 17_600, viewerPoolSize: 50);

        await ctx.Project(ChartType.Single, ChartA);

        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, ChartType.Single, 17_100, 17_850,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADoublesChartDrawsItsPeersOnTheDoublesPool()
    {
        // The two pools sit a thousand apart. The doubles chart asks for the doubles window and
        // nothing of the singles one — the combined total was type-blind, and this is the fix.
        var ctx = new Context(viewerPool: 17_600, viewerPoolSize: 50, viewerDoublesPool: 16_600);

        await ctx.Project(ChartType.Double, ChartA);

        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, ChartType.Double, 16_100, 16_850,
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(It.IsAny<MixEnum>(), ChartType.Single, It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APeerNeedsAFullPoolOfTheTypeToCount()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var full = ctx.WithPeer(poolSize: 50);
        var thin = ctx.WithPeer(poolSize: 49);
        foreach (var peer in new[] { full, thin }) ctx.WithScore(peer, ChartA, 970_000);
        // Four more full-pool peers so the chart clears the five-peer floor either way.
        for (var i = 0; i < 4; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 960_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        // Six candidates in the window, five of them peers; the sixth's score never reached the estimate.
        Assert.Equal(5, result.Group!.Size);
        Assert.Equal(5, result.PeerCount);
        Assert.Equal(PeerGroupKind.PumbilityPeers, result.Group.Kind);
        Assert.Equal(17_500, result.Group.Center); // the viewer's own pool of the type
        Assert.Equal(PeerGroup.PumbilityWindowBelow, result.Group.Below);
        Assert.Equal(PeerGroup.PumbilityWindowAbove, result.Group.Above);
        Assert.Equal(17_000, result.Group.Lowest);
        Assert.Equal(17_750, result.Group.Highest);
    }

    [Fact]
    public async Task TheViewerNeedsAFullPoolOfTheTypeOrTheTypeIsDark()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 29);
        for (var i = 0; i < 6; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000);

        var result = await ctx.Project(ChartType.Double, ChartA);

        Assert.Empty(result.Scores);
        Assert.NotNull(result.Group);
        Assert.False(result.Group!.IsLit);
        Assert.Equal(29, result.Group.PoolCount);
        Assert.Equal(50, result.Group.PoolSize);
        // The window is not swept for a viewer it cannot yet serve: their own pool is read first and
        // alone, and a short one ends the run before anyone else's records are asked for.
        Assert.Equal(0, result.Group.Size);
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(It.IsAny<MixEnum>(), It.IsAny<ChartType>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(),
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 1 && ids.Single() == Viewer), It.IsAny<ChartType>(),
            It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(),
            It.Is<IEnumerable<Guid>>(ids => ids.Count() != 1 || ids.Single() != Viewer), It.IsAny<ChartType>(),
            It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FewerThanFivePeersOnAChartIsNoOpinionAndFiveIsAnEstimate()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        // Four on chart A, five on chart B.
        foreach (var peer in peers.Take(4)) ctx.WithScore(peer, ChartA, 985_000);
        var scores = new[] { 940_000, 985_000, 962_000, 990_000, 975_000 };
        for (var i = 0; i < 5; i++) ctx.WithScore(peers[i], ChartB, scores[i]);

        var result = await ctx.Project(ChartType.Single, ChartA, ChartB);

        Assert.DoesNotContain(ChartA, result.Scores.Keys);
        // The default read is the median (D54): the middle of five equal voices. The quartiles
        // ride the ladder for a caller that asks for them.
        Assert.Equal(975_000, (int)result.Scores[ChartB]);
        Assert.Equal(5, result.PeerCount);
        Assert.Equal(1.0, result.MeanFreshness);
    }

    [Fact]
    public async Task TheLadderCarriesEveryRungTheCallerAskedForAndScoresIsTheFirst()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        var scores = new[] { 940_000, 962_000, 975_000, 985_000, 990_000 };
        for (var i = 0; i < 5; i++) ctx.WithScore(peers[i], ChartA, scores[i]);

        var result = await ctx.ProjectAt(ChartType.Single, new[] { 0.5, 0.25, 0.75 }, ChartA);

        var ladder = result.Ladders![ChartA];
        Assert.Equal(975_000, (int)result.Scores[ChartA]);
        Assert.Equal(956_500, (int)ladder.At(0.25));
        Assert.Equal(975_000, (int)ladder.At(0.5));
        Assert.Equal(986_250, (int)ladder.At(0.75));
        Assert.Equal(5, ladder.PeerCount);
        // Not asked for, so read the default alone: one rung, and Scores is it.
        var bare = await ctx.Project(ChartType.Single, ChartA);
        Assert.Single(bare.Ladders![ChartA].Rungs);
        Assert.Equal((int)bare.Scores[ChartA], (int)bare.Ladders[ChartA].At(PeerEstimator.DefaultQuantile));
    }

    [Fact]
    public async Task AChartWithNoOpinionHasNoLadderEither()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        for (var i = 0; i < 4; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.DoesNotContain(ChartA, result.Scores.Keys);
        Assert.DoesNotContain(ChartA, result.Ladders!.Keys);
    }

    [Fact]
    public async Task ABandTooThinForTheFloorAnswersOnWhatItHasWhenAskedTo()
    {
        // Two peers can never put five voices on a chart, so the floor takes the whole run to
        // nothing rather than filtering it (D47). The ladder still counts the real voices, so a
        // page can say how thin the evidence is.
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 2).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        ctx.WithScore(peers[0], ChartA, 960_000);
        ctx.WithScore(peers[1], ChartA, 980_000);

        var relaxed = await ctx.ProjectRelaxed(ChartType.Single, ChartA);

        // Two equal voices sit at 0.25 and 0.75, so the median reads halfway between them.
        Assert.Equal(970_000, (int)relaxed.Scores[ChartA]);
        Assert.Equal(2, relaxed.Ladders![ChartA].PeerCount);
        Assert.True(relaxed.Group!.AnsweredBelowFloor);
    }

    [Fact]
    public async Task ABandBigEnoughForTheFloorStillSaysSoWhenItsChartsAreThin()
    {
        // The case a size test cannot see, and the reason the flag exists at all. Nine peers is
        // comfortably over the five-peer floor, so nothing about the BAND is thin — but no single
        // chart was scored by five of them, so the run relaxes exactly as a two-peer band does and
        // every row rests on fewer than five voices. A surface warning off Size would say nothing
        // here, about the whole board.
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 9).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        foreach (var peer in peers.Take(4)) ctx.WithScore(peer, ChartA, 970_000);
        foreach (var peer in peers.Skip(4).Take(3)) ctx.WithScore(peer, ChartB, 980_000);

        var relaxed = await ctx.ProjectRelaxed(ChartType.Single, ChartA, ChartB);

        Assert.Equal(9, relaxed.Group!.Size);
        Assert.True(relaxed.Group.AnsweredBelowFloor);
        Assert.Equal(970_000, (int)relaxed.Scores[ChartA]);
        Assert.Equal(980_000, (int)relaxed.Scores[ChartB]);
    }

    [Fact]
    public async Task ARunTheFloorAnsweredIsNotMarkedBelowIt()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000);

        var relaxed = await ctx.ProjectRelaxed(ChartType.Single, ChartA);

        Assert.Equal(970_000, (int)relaxed.Scores[ChartA]);
        Assert.False(relaxed.Group!.AnsweredBelowFloor);
    }

    [Fact]
    public async Task ABandThatScoredNoneOfTheChartsClaimsNoEvidenceEitherWay()
    {
        // The fallback ran and still found nothing, so there is no thin evidence to warn about —
        // only an empty board, which the page already reads as an empty board.
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        for (var i = 0; i < 3; i++) ctx.WithPeer(poolSize: 50);

        var relaxed = await ctx.ProjectRelaxed(ChartType.Single, ChartA);

        Assert.Empty(relaxed.Scores);
        Assert.False(relaxed.Group!.AnsweredBelowFloor);
    }

    [Fact]
    public async Task TheFloorStandsForACallerThatDidNotAskToRelaxIt()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 2).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        foreach (var peer in peers) ctx.WithScore(peer, ChartA, 970_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.Empty(result.Scores);
    }

    [Fact]
    public async Task ATwentyChartPoolProjectsWhenTheCallerSaysWhereItFinishes()
    {
        // The viewer's own gate drops to twenty; a PEER's stays at fifty, because their pool is
        // the evidence. Both peers here hold a full one, so the band is real.
        var ctx = new Context(viewerPool: 4_000, viewerPoolSize: 20);
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 975_000);

        var result = await ctx.ProjectFromFinish(ChartType.Single, 17_500, ChartA);

        Assert.Equal(975_000, (int)result.Scores[ChartA]);
        // Placed by the finish, not by the twenty charts they happen to hold: the window is drawn
        // around 17,500 — 17,000 to 17,750 — and their own standing pool of 4,000 never enters it.
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, ChartType.Single, 17_000, 17_750,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(result.Group!.PlacedByEstimate);
        Assert.Equal(17_500, result.Group.Center);
    }

    [Fact]
    public async Task WithoutAFinishTheGateIsStillTheFullFiftyAndTheGroupSaysSo()
    {
        // The tier list's own call supplies no finish, so a short pool stays dark rather than
        // being seated by the sum of the charts it happens to hold.
        var ctx = new Context(viewerPool: 4_000, viewerPoolSize: 20);
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 975_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.Empty(result.Scores);
        Assert.False(result.Group!.IsLit);
        // The chip counts toward the gate the run was actually made under.
        Assert.Equal(50, result.Group.PoolSize);
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, It.IsAny<ChartType>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnderTwentyChartsAFinishChangesNothingAndTheChipCountsToTwenty()
    {
        var ctx = new Context(viewerPool: 2_000, viewerPoolSize: 12);
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 975_000);

        var result = await ctx.ProjectFromFinish(ChartType.Single, 17_500, ChartA);

        Assert.Empty(result.Scores);
        Assert.False(result.Group!.IsLit);
        Assert.Equal(12, result.Group.PoolCount);
        Assert.Equal(20, result.Group.PoolSize);
    }

    [Fact]
    public async Task TheFallbackRunsOnlyFromZeroAndNeverPerChart()
    {
        // The rescue is all-or-nothing for the run. One chart clearing the floor means the band
        // could answer, so the four-peer chart beside it stays "no opinion" exactly as it would
        // for anyone else — relaxing per chart would quietly lower the bar for a healthy band.
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        foreach (var peer in peers) ctx.WithScore(peer, ChartB, 975_000);
        foreach (var peer in peers.Take(4)) ctx.WithScore(peer, ChartA, 985_000);

        var result = await ctx.ProjectRelaxed(ChartType.Single, ChartA, ChartB);

        Assert.Equal(975_000, (int)result.Scores[ChartB]);
        Assert.DoesNotContain(ChartA, result.Scores.Keys);
    }

    [Fact]
    public async Task Phoenix2ReadsNothingFromPhoenix1AndWeighsNoGrowth()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000 + i * 1_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.True(result.Scores.ContainsKey(ChartA));
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Stats.Verify(s => s.GetStats(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.History.Verify(h => h.GetHistory(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheViewersOwnScoresAreNeverEvidence()
    {
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);
        // Five peers at 960k plus the viewer's own 1,000,000 on the same chart.
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 960_000);
        ctx.WithScore(Viewer, ChartA, 1_000_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.Equal(960_000, (int)result.Scores[ChartA]);
    }

    [Fact]
    public async Task ThePeerReadCoversEveryPricedLevelNotTheTargetsBand()
    {
        // Pool fullness is counted from the same read as the evidence — the viewer's own first,
        // then the band's — so both reads span the whole priced range (10..Max) whatever levels
        // the targets sit at, and neither is ever narrowed to the targets' band.
        var ctx = new Context(viewerPool: 17_500, viewerPoolSize: 50);

        await ctx.Project(ChartType.Single, ChartA);

        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
            ChartType.Single, DifficultyLevel.From(10), DifficultyLevel.Max, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ------------------------------------------------------------------ the peers' pools (§3.10)

    [Fact]
    public async Task ThePeersPoolsRideThePhoenix2RunWhenTheCatalogIsHandedIn()
    {
        var ctx = new Context(viewerPool: 17_609.59, viewerPoolSize: 50);
        var peer = ctx.WithPeer(50);
        var other = ctx.WithPeer(50);
        ctx.WithScore(peer, ChartA, 985_000);
        ctx.WithScore(other, ChartA, 975_000);
        ctx.WithScore(other, ChartB, 990_000);

        var result = await ctx.Project(ChartType.Single, ctx.Catalog(ChartA, ChartB), ChartA);

        var pools = result.PeerPools!;
        Assert.Equal(new[] { peer, other }.Select(PeerVoice.Account).ToHashSet(), pools.Peers);
        // Only the catalog charts are priced into a pool: each peer's real scores are their whole
        // pool here, so A sits at other's #2 (49) and peer's #1 (50), B at other's #1 (50).
        Assert.Equal(2, pools.Charts[ChartA].Holders);
        Assert.Equal(99, pools.Charts[ChartA].Points);
        Assert.Equal(50, pools.Charts[ChartB].Points);
        Assert.Contains(ChartB, pools.Pools[PeerVoice.Account(other)]);
        Assert.DoesNotContain(ChartB, pools.Pools[PeerVoice.Account(peer)]);
        // Two scorers: held, so present, and under the five-peer floor for a projected grade.
        Assert.Equal(2, pools.Charts[ChartA].Scored);
        Assert.Null(pools.Charts[ChartA].ProjectedAt(PeerEstimator.Median));
    }

    [Fact]
    public async Task WithoutTheCatalogThePhoenix2RunReturnsNoPoolsAndStillEstimates()
    {
        var ctx = new Context(viewerPool: 17_609.59, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(50)).ToArray();
        foreach (var peer in peers) ctx.WithScore(peer, ChartA, 980_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.Null(result.PeerPools);
        Assert.Equal(980_000, (int)result.Scores[ChartA]);
    }

    [Fact]
    public async Task TheViewerIsNotInTheirOwnPeersPools()
    {
        var ctx = new Context(viewerPool: 17_609.59, viewerPoolSize: 50);
        var peer = ctx.WithPeer(50);
        ctx.WithScore(peer, ChartA, 985_000);
        ctx.WithScore(Viewer, ChartA, 999_000);

        var result = await ctx.Project(ChartType.Single, ctx.Catalog(ChartA), ChartA);

        Assert.DoesNotContain(PeerVoice.Account(Viewer), result.PeerPools!.Peers);
        Assert.Equal(1, result.PeerPools.Charts[ChartA].Holders);
        Assert.Equal(1, result.PeerPools.Charts[ChartA].Scored);
    }

    [Fact]
    public async Task Phoenix1FillsPoolsFromTheBandWhenTheCatalogIsHandedIn()
    {
        // D43: the band is the peer group, nobody gated on a pool, and the pools are priced with
        // Phoenix 1 scoring — a SSS+ on a level 22 is Base(22) 880 × 1.5 = 1,320 there.
        var ctx = new Context(viewerPool: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);
        var peer = ctx.WithPhoenix1Peer(21.0);
        var thin = ctx.WithPhoenix1Peer(20.6);
        ctx.WithPhoenix1Score(peer, ChartA, 970_000);
        ctx.WithPhoenix1Score(peer, ChartB, 998_000);
        ctx.WithPhoenix1Score(thin, ChartA, 960_000);

        var result = await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ctx.Catalog(MixEnum.Phoenix, ChartA, ChartB), ChartA);

        var pools = result.PeerPools!;
        Assert.Equal(new[] { peer, thin }.Select(PeerVoice.Account).ToHashSet(), pools.Peers);
        Assert.Equal(2, pools.Charts[ChartA].Holders);
        // A at peer's #2 (49) and thin's #1 (50); B at peer's #1 (50). A thin pool casts a shorter vote, not none.
        Assert.Equal(99, pools.Charts[ChartA].Points);
        Assert.Equal(50, pools.Charts[ChartB].Points);
        Assert.Equal(2, result.Group!.Size);
        // The estimate itself is the competitive band's, to the score, exactly as without the catalog.
        var bare = await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ChartA);
        Assert.Equal((int)bare.Scores[ChartA], (int)result.Scores[ChartA]);
        Assert.Equal(bare.MeanFreshness, result.MeanFreshness);
    }

    [Fact]
    public async Task Phoenix1ReadsToThePoolFloorOnlyWhenItIsBuildingPools()
    {
        var ctx = new Context(viewerPool: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);
        var peer = ctx.WithPhoenix1Peer(21.0);
        ctx.WithPhoenix1Score(peer, ChartA, 970_000);

        await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ChartA);
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
            ChartType.Single, DifficultyLevel.From(22), DifficultyLevel.From(22), It.IsAny<CancellationToken>()), Times.Once);

        await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ctx.Catalog(MixEnum.Phoenix, ChartA), ChartA);
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
            ChartType.Single, DifficultyLevel.From(10), DifficultyLevel.Max, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APhoenix1BandWithNoVoiceOnTheTargetsStillHandsBackItsPools()
    {
        // A band that scored none of the charts asked about has no estimate to give, but its
        // pools are real and the page prints them — the no-opinion early return keeps them.
        var ctx = new Context(viewerPool: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);
        var peer = ctx.WithPhoenix1Peer(21.0);
        ctx.WithPhoenix1Score(peer, ChartB, 990_000);

        var result = await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ctx.Catalog(MixEnum.Phoenix, ChartA, ChartB), ChartA);

        Assert.Empty(result.Scores);
        Assert.Equal(1, result.PeerPools!.Charts[ChartB].Holders);
        Assert.Equal(1, result.Group!.Size);
    }

    // ------------------------------------------------------------------ Phoenix 1

    [Fact]
    public async Task Phoenix1ReadsOnlyPhoenix1AndNamesItsCompetitiveBand()
    {
        var ctx = new Context(viewerPool: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);
        var peer = ctx.WithPhoenix1Peer(21.0);
        ctx.WithPhoenix1Score(peer, ChartA, 970_000);

        var result = await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ChartA);

        Assert.Equal(970_000, (int)result.Scores[ChartA]);
        Assert.Equal(1, result.Ladders![ChartA].PeerCount);
        Assert.Equal(970_000, (int)result.Ladders[ChartA].At(PeerEstimator.DefaultQuantile));
        Assert.Equal(PeerGroupKind.CompetitiveBand, result.Group!.Kind);
        Assert.Equal(21.4, result.Group.Center);
        Assert.Equal(1.0, result.Group.Below);
        Assert.Equal(1.0, result.Group.Above);
        Assert.True(result.Group.IsLit);
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(It.IsAny<MixEnum>(), It.IsAny<ChartType>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APhoenix2CompetitiveLevelIsNeverBorrowedFromPhoenix1()
    {
        // The launch fallback that read the other mix's level is gone (D21): a Phoenix 2 account
        // at the no-data floor stays at the floor.
        var ctx = new Context(viewerPool: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);

        var level = await ctx.Projector.CompetitiveLevel(MixEnum.Phoenix2, ChartType.Single, Viewer,
            CancellationToken.None);

        Assert.Equal(1, level);
        ctx.Stats.Verify(s => s.GetStats(MixEnum.Phoenix, Viewer, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------ fixture

    private sealed class Context
    {
        private readonly Dictionary<Guid, int> _phoenix2PoolSizes = new();
        private readonly Dictionary<Guid, double> _phoenix1Levels = new();
        private readonly List<UserPhoenixScore> _phoenix2Scores = new();
        private readonly List<UserPhoenixScore> _phoenix1Scores = new();
        private readonly double _viewerPool;
        private readonly double _viewerDoublesPool;
        private readonly double _phoenix1SinglesLevel;

        /// <param name="viewerPool">
        ///     The viewer's Phoenix 2 pool of the type off their stats row — singles and doubles
        ///     alike unless <paramref name="viewerDoublesPool" /> says otherwise (D53).
        /// </param>
        public Context(double viewerPool, int viewerPoolSize, double phoenix1SinglesLevel = 1,
            double? viewerDoublesPool = null)
        {
            _viewerPool = viewerPool;
            _viewerDoublesPool = viewerDoublesPool ?? viewerPool;
            _phoenix1SinglesLevel = phoenix1SinglesLevel;
            _phoenix2PoolSizes[Viewer] = viewerPoolSize;

            Stats.Setup(s => s.GetStats(MixEnum.Phoenix2, Viewer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => StatsFor(Viewer, _viewerPool, _viewerDoublesPool, 1));
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Guid id, CancellationToken _) =>
                    StatsFor(id, 0, 0, id == Viewer ? _phoenix1SinglesLevel : _phoenix1Levels.GetValueOrDefault(id, 1)));
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => StatsFor(id, 0, 0, _phoenix1Levels.GetValueOrDefault(id, 1))).ToArray().AsEnumerable());
            Stats.Setup(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, It.IsAny<ChartType>(), It.IsAny<double>(),
                    It.IsAny<double>(), It.IsAny<CancellationToken>()))
                // Every peer this fixture creates sits inside the viewer's window; the window's
                // edges are asserted on the call itself, not simulated here.
                .ReturnsAsync(() => _phoenix2PoolSizes.Keys.Where(id => id != Viewer).ToArray().AsEnumerable());
            Stats.Setup(s => s.GetPlayersByCompetitiveRange(MixEnum.Phoenix, It.IsAny<ChartType?>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _phoenix1Levels.Keys.ToArray().AsEnumerable());

            Scores.Setup(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, IEnumerable<Guid> ids, ChartType _, DifficultyLevel _,
                    DifficultyLevel _, CancellationToken _) =>
                {
                    var asked = ids.ToHashSet();
                    if (mix == MixEnum.Phoenix)
                        return _phoenix1Scores.Where(s => asked.Contains(s.UserId)).ToArray().AsEnumerable();
                    // Phoenix 2: the scores on the charts under test, plus filler records that
                    // stand in for the rest of each player's pool — distinct chart ids, so the
                    // projector's own count of the pool reads the size the test declared.
                    return asked.SelectMany(id => _phoenix2Scores.Where(s => s.UserId == id)
                            .Concat(Filler(id)))
                        .ToArray().AsEnumerable();
                });

            History.Setup(h => h.GetHistory(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PlayerRatingRecord>());

            Projector = new ScoreProjector(Scores.Object, Stats.Object, History.Object);
        }

        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerHistoryRepository> History { get; } = new();
        public ScoreProjector Projector { get; }

        public Guid WithPeer(int poolSize)
        {
            var id = Guid.NewGuid();
            _phoenix2PoolSizes[id] = poolSize;
            return id;
        }

        public void WithScore(Guid user, Guid chart, int score)
        {
            _phoenix2Scores.Add(new UserPhoenixScore(user, chart, "Peer", score, PhoenixPlate.MarvelousGame, false));
        }

        public Guid WithPhoenix1Peer(double singlesLevel)
        {
            var id = Guid.NewGuid();
            _phoenix1Levels[id] = singlesLevel;
            return id;
        }

        public void WithPhoenix1Score(Guid user, Guid chart, int score)
        {
            _phoenix1Scores.Add(new UserPhoenixScore(user, chart, "Peer", score, PhoenixPlate.MarvelousGame, false,
                true, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)));
        }

        public Task<ScoreProjection> Project(ChartType type, params Guid[] charts)
        {
            return Project(MixEnum.Phoenix2, type, 1.0, null, charts);
        }

        public Task<ScoreProjection> Project(ChartType type, IReadOnlyDictionary<Guid, Chart> catalog,
            params Guid[] charts)
        {
            return Project(MixEnum.Phoenix2, type, 1.0, catalog, charts);
        }

        public Task<ScoreProjection> Project(MixEnum mix, ChartType type, double window, params Guid[] charts)
        {
            return Project(mix, type, window, null, charts);
        }

        public Task<ScoreProjection> Project(MixEnum mix, ChartType type, double window,
            IReadOnlyDictionary<Guid, Chart>? catalog, params Guid[] charts)
        {
            return Projector.Project(new ScoreProjectionRequest(mix, type, Viewer,
                charts.Select(c => new ProjectionTarget(c, 22)).ToArray(), window, catalog), CancellationToken.None);
        }

        /// <summary>A caller that lets the player choose a rung: the same request naming the rungs it will read (D51).</summary>
        public Task<ScoreProjection> ProjectAt(ChartType type, double[] quantiles, params Guid[] charts)
        {
            return Projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, type, Viewer,
                    charts.Select(c => new ProjectionTarget(c, 22)).ToArray(), 1.0, Quantiles: quantiles),
                CancellationToken.None);
        }

        /// <summary>The PUMBILITY caller's run: the same request asking for the thin-band fallback (D47).</summary>
        public Task<ScoreProjection> ProjectRelaxed(ChartType type, params Guid[] charts)
        {
            return Projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, type, Viewer,
                    charts.Select(c => new ProjectionTarget(c, 22)).ToArray(), 1.0, null, RelaxFloorWhenEmpty: true),
                CancellationToken.None);
        }

        /// <summary>The PUMBILITY caller's run for a short pool: placed and gated by an extrapolated finish (D48).</summary>
        public Task<ScoreProjection> ProjectFromFinish(ChartType type, double finishedTotal, params Guid[] charts)
        {
            return Projector.Project(new ScoreProjectionRequest(MixEnum.Phoenix2, type, Viewer,
                    charts.Select(c => new ProjectionTarget(c, 22)).ToArray(), 1.0, null,
                    ProjectedTotal: finishedTotal, ProjectedTotalIsEstimate: true),
                CancellationToken.None);
        }

        /// <summary>A Phoenix 2 catalog of the given singles charts at level 22 — the pools price against it.</summary>
        public IReadOnlyDictionary<Guid, Chart> Catalog(params Guid[] charts) => Catalog(MixEnum.Phoenix2, charts);

        public IReadOnlyDictionary<Guid, Chart> Catalog(MixEnum mix, params Guid[] charts)
        {
            return charts.ToDictionary(id => id, id => new ChartBuilder().WithId(id).WithMix(mix)
                .WithType(ChartType.Single).WithLevel(22).Build());
        }

        /// <summary>
        ///     Distinct filler charts making up the rest of a declared pool. A player declared at
        ///     50 with two real scores gets 48 fillers; one declared at 0 gets none.
        /// </summary>
        private IEnumerable<UserPhoenixScore> Filler(Guid id)
        {
            var real = _phoenix2Scores.Count(s => s.UserId == id);
            var wanted = Math.Max(0, _phoenix2PoolSizes.GetValueOrDefault(id) - real);
            return Enumerable.Range(0, wanted).Select(i =>
                new UserPhoenixScore(id, FillerChart(id, i), "Peer", 950_000, PhoenixPlate.FairGame, false));
        }

        private static Guid FillerChart(Guid user, int index)
        {
            // Deterministic per (user, index) so the same filler chart is never counted twice.
            var bytes = user.ToByteArray();
            bytes[0] = (byte)(index & 0xFF);
            bytes[1] = (byte)((index >> 8) & 0xFF);
            bytes[2] ^= 0x5A;
            return new Guid(bytes);
        }

        /// <summary>A stats row carrying the two per-type pools (and their sum as the merged total) and one competitive level.</summary>
        private static PlayerStatsRecord StatsFor(Guid id, double singlesPool, double doublesPool, double singlesLevel)
        {
            return new PlayerStatsRecord(id, 0, 1, 0, 0, 0, singlesPool + doublesPool, 0, 0, singlesPool, 0, 0,
                doublesPool, 0, 0, singlesLevel, singlesLevel, singlesLevel);
        }
    }
}
