using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PumbilityProjectionSagaTests
{
    [Fact]
    public async Task AChartNobodyComparableHasPlayedGetsNoProjection()
    {
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.DoesNotContain(chart.Id, result.ExpectedScores.Keys);
        Assert.DoesNotContain(chart.Id, result.ProjectedGains.Keys);
    }

    [Fact]
    public async Task TheEstimateComesFromThePeersWhoPlayedIt()
    {
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 900_000, 940_000, 950_000, 960_000, 970_000, 980_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.True(result.ExpectedScores.ContainsKey(chart.Id));
        Assert.InRange((int)result.ExpectedScores[chart.Id], 900_000, 980_000);
    }

    [Fact]
    public async Task PeersWhoHaveOutgrownTheirScoreCountForLess()
    {
        // The same six scores twice. In the second run the low scorers set theirs three
        // levels ago, so discounting them must raise the estimate.
        var flat = new ProjectionContext().WithChart(out var chartA, ChartType.Single, 20);
        flat.WithPeerScores(chartA, 900_000, 905_000, 910_000, 970_000, 975_000, 980_000);
        var flatResult = await flat.Saga.Handle(new ProjectPumbilityGainsQuery(flat.UserId), CancellationToken.None);

        var grown = new ProjectionContext().WithChart(out var chartB, ChartType.Single, 20);
        grown.WithPeerScore(chartB, 900_000, levelsGrownSince: 3)
            .WithPeerScore(chartB, 905_000, levelsGrownSince: 3)
            .WithPeerScore(chartB, 910_000, levelsGrownSince: 3)
            .WithPeerScore(chartB, 970_000)
            .WithPeerScore(chartB, 975_000)
            .WithPeerScore(chartB, 980_000);
        var grownResult =
            await grown.Saga.Handle(new ProjectPumbilityGainsQuery(grown.UserId), CancellationToken.None);

        Assert.True((int)grownResult.ExpectedScores[chartB.Id] > (int)flatResult.ExpectedScores[chartA.Id],
            "discounting outgrown scores should raise the estimate");
    }

    [Fact]
    public async Task EvidenceReportsVoicesAndSpreadNotJustHeadcount()
    {
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScore(chart, 910_000, levelsGrownSince: 4)
            .WithPeerScore(chart, 950_000)
            .WithPeerScore(chart, 990_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        var evidence = result.Evidence[chart.Id];
        Assert.Equal(3, evidence.PeerCount);
        Assert.True(evidence.EffectivePeers < evidence.PeerCount,
            "an outgrown peer should be worth less than a whole voice");
        Assert.True(evidence.Spread > 0, "three different scores should report a spread");
    }

    [Fact]
    public async Task ChartsOutsideTheScoringLevelWindowAreNotProjected()
    {
        // Competitive level 20, window +/-2: a chart scoring at 24 is out of scope even
        // though peers have played it.
        var ctx = new ProjectionContext(20)
            .WithChart(out var far, ChartType.Single, 24, 24.0);
        ctx.WithPeerScores(far, 950_000, 955_000, 960_000, 965_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.DoesNotContain(far.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task AChartIsScopedByItsScoringLevelNotItsPrintedLevel()
    {
        // Printed 24, but the community scores it like a 21 — inside a level-20 player's
        // window. Scoping on the printed number would wrongly drop it.
        var ctx = new ProjectionContext(20)
            .WithChart(out var overrated, ChartType.Single, 24, 21.0);
        ctx.WithPeerScores(overrated, 950_000, 955_000, 960_000, 965_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Contains(overrated.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task AskingForOnePoolProjectsOnlyThatType()
    {
        var ctx = new ProjectionContext()
            .WithChart(out var single, ChartType.Single, 20)
            .WithChart(out var doubles, ChartType.Double, 20);
        ctx.WithPeerScores(single, 950_000, 955_000, 960_000, 965_000);
        ctx.WithPeerScores(doubles, 950_000, 955_000, 960_000, 965_000);

        var result = await ctx.Saga.Handle(
            new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix, ChartType.Single),
            CancellationToken.None);

        Assert.Contains(single.Id, result.ExpectedScores.Keys);
        Assert.DoesNotContain(doubles.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task AGainIsWhatTheChartAddsOverTheChartItWouldDisplace()
    {
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 980_000, 985_000, 990_000, 995_000);
        ctx.WithFullPoolAt(950_000, ChartType.Single, 18);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var projected = result.ExpectedScores[chart.Id];
        var expected = (int)(scoring.GetScore(chart, projected,
                                 ScoringConfiguration.ExpectedPlateForScore(projected), false)
                             - ctx.PoolBaseline(scoring));
        Assert.Equal(expected, result.ProjectedGains[chart.Id]);
    }

    [Fact]
    public async Task AScoredChartOutsideThePoolIsPricedAgainstTheBarNotItsOwnValue()
    {
        // A chart you have played but that sits below your 50th displaces the BAR when it
        // improves, not its own old value. Pricing it against itself inflates the gain by
        // the gap, and every such chart out-ranks the honest ones on the list.
        var ctx = new ProjectionContext().WithChart(out var weak, ChartType.Single, 20);
        ctx.WithPeerScores(weak, 985_000, 988_000, 990_000, 995_000);
        ctx.WithPoolAndTail(inPool: 950_000, belowBar: 905_000, ChartType.Single, 20, weak);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var projected = result.ExpectedScores[weak.Id];
        var expected = (int)(scoring.GetScore(weak, projected,
                                 ScoringConfiguration.ExpectedPlateForScore(projected), false)
                             - ctx.PoolBaseline(scoring));
        Assert.Equal(expected, result.ProjectedGains[weak.Id]);
    }

    [Fact]
    public async Task AChartThatCannotClearTheBarIsNotOffered()
    {
        // A weak 15 against a pool of 22s: the projection exists, the gain does not.
        var ctx = new ProjectionContext(17).WithChart(out var weak, ChartType.Single, 15);
        ctx.WithPeerScores(weak, 910_000, 915_000, 920_000, 925_000);
        ctx.WithFullPoolAt(990_000, ChartType.Single, 22);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Contains(weak.Id, result.ExpectedScores.Keys);
        Assert.DoesNotContain(weak.Id, result.ProjectedGains.Keys);
    }

    [Fact]
    public async Task APlayerWithNoCompetitiveLevelGetsNothingRatherThanNonsense()
    {
        var ctx = new ProjectionContext(1, 1).WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Empty(result.ExpectedScores);
    }

    // ------------------------------------------------------------------ context

    private sealed class ProjectionContext
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly List<Chart> _charts = new();
        private readonly double _doubles;
        private readonly List<PlayerRatingRecord> _peerHistory = new();
        private readonly Dictionary<Guid, double> _peerLevelNow = new();
        private readonly List<UserPhoenixScore> _peerScores = new();
        private readonly Dictionary<Guid, double> _scoringLevels = new();
        private readonly double _singles;
        private readonly List<RecordedPhoenixScore> _topScores = new();

        public ProjectionContext(double singlesCompetitive = 20, double doublesCompetitive = 20)
        {
            _singles = singlesCompetitive;
            _doubles = doublesCompetitive;

            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.Is<Guid>(g => g == UserId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => StatsFor(UserId, _singles, _doubles));
            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _peerLevelNow
                    .Select(kv => StatsFor(kv.Key, kv.Value, kv.Value)).ToArray().AsEnumerable());
            Stats.Setup(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _peerLevelNow.Keys.ToArray().AsEnumerable());

            History.Setup(h => h.GetHistory(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _peerHistory.ToArray().AsEnumerable());

            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, IEnumerable<Guid> _, IEnumerable<Guid> chartIds, CancellationToken _) =>
                {
                    var wanted = chartIds.ToHashSet();
                    return _peerScores.Where(p => wanted.Contains(p.ChartId)).ToArray().AsEnumerable();
                });

            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _charts.ToArray().AsEnumerable());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (IDictionary<Guid, double>)new Dictionary<Guid, double>(_scoringLevels));
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<IEnumerable<RecordedPhoenixScore>> request, CancellationToken _) =>
                {
                    var query = (GetTop50ForPlayerQuery)request;
                    return _topScores.Where(t => query.ChartType == null ||
                                                 _charts.First(c => c.Id == t.ChartId).Type == query.ChartType);
                });
            Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Array.Empty<SongTierListEntry>().AsEnumerable());

            Saga = new PumbilityProjectionSaga(Mediator.Object, Stats.Object, Scores.Object, History.Object);
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerHistoryRepository> History { get; } = new();
        public PumbilityProjectionSaga Saga { get; }

        public ProjectionContext WithChart(out Chart chart, ChartType type, int level, double? scoringLevel = null)
        {
            chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
                new Song($"Song {_charts.Count}", SongType.Arcade, new Uri("https://piu.test/i.png"),
                    TimeSpan.FromMinutes(2), "Artist", 180),
                type, level, MixEnum.Phoenix, null, null, new HashSet<Skill>());
            _charts.Add(chart);
            _scoringLevels[chart.Id] = scoringLevel ?? level;
            return this;
        }

        public ProjectionContext WithPeerScores(Chart chart, params int[] scores)
        {
            foreach (var score in scores) WithPeerScore(chart, score);
            return this;
        }

        public ProjectionContext WithPeerScore(Chart chart, int score, double levelsGrownSince = 0)
        {
            var peer = Guid.NewGuid();
            var levelNow = chart.Type == ChartType.Single ? _singles : _doubles;
            _peerLevelNow[peer] = levelNow;
            var recordedAt = Now.AddDays(-100);
            _peerScores.Add(new UserPhoenixScore(peer, chart.Id, "Peer", score, PhoenixPlate.MarvelousGame,
                false, true, recordedAt));
            // One history row dated before the score: the level they held when they set it.
            var then = levelNow - levelsGrownSince;
            _peerHistory.Add(new PlayerRatingRecord(peer, recordedAt.AddDays(-1), then, then, then, 0, 0));
            return this;
        }

        /// <summary>
        ///     A full pool plus a tail below the bar, with <paramref name="tailChart" /> among
        ///     the tail — the shape that exposes pricing a below-bar chart against itself.
        ///     GetTop50ForPlayerQuery returns 100, so ranks 51-100 reach the saga.
        /// </summary>
        public ProjectionContext WithPoolAndTail(int inPool, int belowBar, ChartType type, int level,
            Chart tailChart)
        {
            WithFullPoolAt(inPool, type, level);
            _topScores.Add(new RecordedPhoenixScore(tailChart.Id, belowBar, PhoenixPlate.FairGame, false,
                Now.AddDays(-300)));
            return this;
        }

        /// <summary>Fills the top-50 pool so gains price against a real bar rather than zero.</summary>
        public ProjectionContext WithFullPoolAt(int score, ChartType type, int level)
        {
            for (var i = 0; i < 50; i++)
            {
                WithChart(out var filler, type, level);
                _topScores.Add(new RecordedPhoenixScore(filler.Id, score, PhoenixPlate.MarvelousGame, false,
                    Now.AddDays(-200)));
            }

            return this;
        }

        public double PoolBaseline(ScoringConfiguration scoring)
        {
            if (_topScores.Count < 50) return 0;
            return _topScores
                .Select(t => (int)scoring.GetScore(_charts.First(c => c.Id == t.ChartId), t.Score!.Value,
                    t.Plate ?? PhoenixPlate.RoughGame, t.IsBroken))
                .OrderByDescending(v => v).Take(50).Min();
        }

        private static PlayerStatsRecord StatsFor(Guid userId, double singles, double doubles)
        {
            return new PlayerStatsRecord(userId, 0, 1, 0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                (singles + doubles) / 2,
                singles,
                doubles);
        }
    }
}
