using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The projector's plumbing, mix by mix: who it asks for, what it reads, and what it refuses
///     to read. Phoenix 2 draws PUMBILITY peers (docs/design/pumbility-overhaul.md §4.8) and
///     never touches Phoenix 1; Phoenix 1 draws a competitive band and never touches Phoenix 2.
///     The arithmetic itself is PeerEstimatorTests' business.
/// </summary>
public sealed class ScoreProjectorTests
{
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    // ------------------------------------------------------------------ Phoenix 2

    [Fact]
    public async Task Phoenix2PeersAreTheRungBandOnTheTotalPool()
    {
        // DIAMOND LV.4 (17,609.59) reaches down to DIAMOND LV.1 (17,000) and up to RED BERYL LV.2,
        // whose next rung starts at 18,400 — the range asked of the stats reader, half-open.
        var ctx = new Context(viewerTotal: 17_609.59, viewerPoolSize: 50);

        await ctx.Project(ChartType.Single, ChartA);

        ctx.Stats.Verify(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, 17_000, 18_400,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThePeerBandIsOpenEndedAtTheCapstoneAndFlooredAtZero()
    {
        var abyss = new Context(viewerTotal: 20_050, viewerPoolSize: 50);
        await abyss.Project(ChartType.Single, ChartA);
        // ALEXANDRITE LV.3 (index 33, 19,400) is three rungs under the capstone; nothing sits above it.
        abyss.Stats.Verify(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, 19_400, double.MaxValue,
            It.IsAny<CancellationToken>()), Times.Once);

        var unranked = new Context(viewerTotal: 9_000, viewerPoolSize: 50);
        await unranked.Project(ChartType.Single, ChartA);
        // Index 0 minus three is still index 0; three above it is BRONZE LV.3, and BRONZE LV.4 starts at 11,500.
        unranked.Stats.Verify(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, 0, 11_500,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APeerNeedsAFullPoolOfTheTypeToCount()
    {
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
        var full = ctx.WithPeer(poolSize: 50);
        var thin = ctx.WithPeer(poolSize: 49);
        foreach (var peer in new[] { full, thin }) ctx.WithScore(peer, ChartA, 970_000);
        // Four more full-pool peers so the chart clears the five-peer floor either way.
        for (var i = 0; i < 4; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 960_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        // Six candidates in the band, five of them peers; the sixth's score never reached the estimate.
        Assert.Equal(5, result.Group!.Size);
        Assert.Equal(5, result.PeerCount);
        Assert.Equal(PeerGroupKind.PumbilityBand, result.Group.Kind);
        Assert.Equal(23, result.Group.Center); // 17,500 is DIAMOND LV.3, badge index 23
        Assert.Equal(3, result.Group.HalfWidth);
    }

    [Fact]
    public async Task TheViewerNeedsAFullPoolOfTheTypeOrTheTypeIsDark()
    {
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 29);
        for (var i = 0; i < 6; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000);

        var result = await ctx.Project(ChartType.Double, ChartA);

        Assert.Empty(result.Scores);
        Assert.NotNull(result.Group);
        Assert.False(result.Group!.IsLit);
        Assert.Equal(29, result.Group.PoolCount);
        Assert.Equal(50, result.Group.PoolSize);
        // The peers still exist and are still counted, so the page can say what would light up.
        Assert.Equal(6, result.Group.Size);
    }

    [Fact]
    public async Task FewerThanFivePeersOnAChartIsNoOpinionAndFiveIsTheMedian()
    {
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        // Four on chart A, five on chart B.
        foreach (var peer in peers.Take(4)) ctx.WithScore(peer, ChartA, 985_000);
        var scores = new[] { 940_000, 985_000, 962_000, 990_000, 975_000 };
        for (var i = 0; i < 5; i++) ctx.WithScore(peers[i], ChartB, scores[i]);

        var result = await ctx.Project(ChartType.Single, ChartA, ChartB);

        Assert.DoesNotContain(ChartA, result.Scores.Keys);
        Assert.Equal(975_000, (int)result.Scores[ChartB]);
        Assert.Equal(5, result.PeerCount);
        Assert.Equal(1.0, result.MeanFreshness);
    }

    [Fact]
    public async Task TheSpreadBracketsTheMedianWithTheSamePeers()
    {
        // The Peers IQR: first and third quartiles of the same five voices the median came from,
        // read with the same midpoint-convention quantile, and how many peers voted.
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
        var peers = Enumerable.Range(0, 5).Select(_ => ctx.WithPeer(poolSize: 50)).ToArray();
        var scores = new[] { 940_000, 962_000, 975_000, 985_000, 990_000 };
        for (var i = 0; i < 5; i++) ctx.WithScore(peers[i], ChartA, scores[i]);

        var result = await ctx.Project(ChartType.Single, ChartA);

        var spread = result.Spreads![ChartA];
        Assert.Equal(975_000, (int)result.Scores[ChartA]);
        // Midpoint positions of five equal voices are 0.1, 0.3, 0.5, 0.7, 0.9: q25 interpolates
        // three-quarters of the way from 940k to 962k, q75 a quarter of the way from 985k to 990k.
        Assert.Equal(956_500, (int)spread.Quartile1);
        Assert.Equal(986_250, (int)spread.Quartile3);
        Assert.Equal(5, spread.PeerCount);
        Assert.True((int)spread.Quartile1 <= (int)result.Scores[ChartA]);
        Assert.True((int)spread.Quartile3 >= (int)result.Scores[ChartA]);
    }

    [Fact]
    public async Task AChartWithNoOpinionHasNoSpreadEither()
    {
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
        for (var i = 0; i < 4; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 970_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.DoesNotContain(ChartA, result.Scores.Keys);
        Assert.DoesNotContain(ChartA, result.Spreads!.Keys);
    }

    [Fact]
    public async Task Phoenix2ReadsNothingFromPhoenix1AndWeighsNoGrowth()
    {
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
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
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);
        // Five peers at 960k plus the viewer's own 1,000,000 on the same chart.
        for (var i = 0; i < 5; i++) ctx.WithScore(ctx.WithPeer(poolSize: 50), ChartA, 960_000);
        ctx.WithScore(Viewer, ChartA, 1_000_000);

        var result = await ctx.Project(ChartType.Single, ChartA);

        Assert.Equal(960_000, (int)result.Scores[ChartA]);
    }

    [Fact]
    public async Task ThePeerReadCoversEveryPricedLevelNotTheTargetsBand()
    {
        // Pool fullness is counted from the same read as the evidence, so the read spans the
        // whole priced range (10..Max) whatever levels the targets sit at.
        var ctx = new Context(viewerTotal: 17_500, viewerPoolSize: 50);

        await ctx.Project(ChartType.Single, ChartA);

        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
            ChartType.Single, DifficultyLevel.From(10), DifficultyLevel.Max, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------ Phoenix 1

    [Fact]
    public async Task Phoenix1ReadsOnlyPhoenix1AndNamesItsCompetitiveBand()
    {
        var ctx = new Context(viewerTotal: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);
        var peer = ctx.WithPhoenix1Peer(21.0);
        ctx.WithPhoenix1Score(peer, ChartA, 970_000);

        var result = await ctx.Project(MixEnum.Phoenix, ChartType.Single, 1.0, ChartA);

        Assert.Equal(970_000, (int)result.Scores[ChartA]);
        Assert.Equal(1, result.Spreads![ChartA].PeerCount);
        Assert.Equal(970_000, (int)result.Spreads[ChartA].Quartile1);
        Assert.Equal(PeerGroupKind.CompetitiveBand, result.Group!.Kind);
        Assert.Equal(21.4, result.Group.Center);
        Assert.Equal(1.0, result.Group.HalfWidth);
        Assert.True(result.Group.IsLit);
        ctx.Stats.Verify(s => s.GetPlayersByPumbilityRange(It.IsAny<MixEnum>(), It.IsAny<double>(),
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
        var ctx = new Context(viewerTotal: 0, viewerPoolSize: 0, phoenix1SinglesLevel: 21.4);

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
        private readonly double _viewerTotal;
        private readonly double _phoenix1SinglesLevel;

        public Context(double viewerTotal, int viewerPoolSize, double phoenix1SinglesLevel = 1)
        {
            _viewerTotal = viewerTotal;
            _phoenix1SinglesLevel = phoenix1SinglesLevel;
            _phoenix2PoolSizes[Viewer] = viewerPoolSize;

            Stats.Setup(s => s.GetStats(MixEnum.Phoenix2, Viewer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => StatsFor(Viewer, _viewerTotal, 1));
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Guid id, CancellationToken _) =>
                    StatsFor(id, 0, id == Viewer ? _phoenix1SinglesLevel : _phoenix1Levels.GetValueOrDefault(id, 1)));
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => StatsFor(id, 0, _phoenix1Levels.GetValueOrDefault(id, 1))).ToArray().AsEnumerable());
            Stats.Setup(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, It.IsAny<double>(), It.IsAny<double>(),
                    It.IsAny<CancellationToken>()))
                // Every peer this fixture creates sits inside the viewer's band; the band's edges
                // are asserted on the call itself, not simulated here.
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
            return Project(MixEnum.Phoenix2, type, 1.0, charts);
        }

        public Task<ScoreProjection> Project(MixEnum mix, ChartType type, double window, params Guid[] charts)
        {
            return Projector.Project(new ScoreProjectionRequest(mix, type, Viewer,
                charts.Select(c => new ProjectionTarget(c, 22)).ToArray(), window), CancellationToken.None);
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

        private static PlayerStatsRecord StatsFor(Guid id, double total, double singlesLevel)
        {
            return new PlayerStatsRecord(id, 0, 1, 0, 0, 0, total, 0, 0, 0, 0, 0, 0, 0, 0, singlesLevel, singlesLevel,
                singlesLevel);
        }
    }
}
