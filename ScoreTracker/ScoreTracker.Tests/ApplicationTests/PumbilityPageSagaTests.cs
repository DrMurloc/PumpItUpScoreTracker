using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PumbilityPageSagaTests
{
    [Fact]
    public async Task TheBarIsWhatTheFiftiethChartIsWorth()
    {
        var ctx = new PageContext().WithPool(60, ChartType.Single, 20);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(50, page.Pool.Count);
        Assert.Equal(page.Pool[^1].Value, page.Bar);
        Assert.Equal(page.Pool[^1].ChartId, page.BarChartId);
    }

    [Fact]
    public async Task AnUnfilledPoolHasNoBarBecauseNothingIsBeingDisplaced()
    {
        var ctx = new PageContext().WithPool(12, ChartType.Single, 20);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.Null(page.Bar);
        Assert.Null(page.BarChartId);
        Assert.Equal(12, page.Pool.Count);
    }

    [Fact]
    public async Task TheWaitingRoomIsWhatHasToCrossTheLine()
    {
        var ctx = new PageContext().WithPool(60, ChartType.Single, 20);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(6, page.WaitingRoom.Count);
        Assert.Equal(51, page.WaitingRoom[0].Place);
        Assert.True(page.WaitingRoom.All(w => w.Value <= page.Bar),
            "nothing in the waiting room may out-rank the bar");
    }

    [Fact]
    public async Task ThePoolReportsHowFlatItIs()
    {
        var ctx = new PageContext().WithPool(55, ChartType.Single, 20);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.True(page.PoolTop >= page.PoolBottom);
        Assert.InRange(page.PoolSpread, 0, 1);
        Assert.Equal(page.Pool.Sum(p => p.Value), page.Total);
    }

    [Fact]
    public async Task ATargetKnowsWhetherYouAlreadyHoldTheChart()
    {
        var ctx = new PageContext().WithPool(55, ChartType.Single, 20);
        ctx.WithTarget(out var upgrade, gain: 400, projected: 985_000, current: 930_000)
            .WithTarget(out var fresh, gain: 300, projected: 960_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        var upgradeRow = page.Targets.Single(t => t.ChartId == upgrade);
        var freshRow = page.Targets.Single(t => t.ChartId == fresh);
        Assert.NotNull(upgradeRow.Current);
        Assert.Null(freshRow.Current);
    }

    [Fact]
    public async Task TargetsComeBackBestGainFirst()
    {
        var ctx = new PageContext().WithPool(55, ChartType.Single, 20);
        ctx.WithTarget(out _, gain: 120, projected: 950_000)
            .WithTarget(out var biggest, gain: 900, projected: 990_000)
            .WithTarget(out _, gain: 400, projected: 970_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(biggest, page.Targets[0].ChartId);
        Assert.True(page.Targets.Select(t => t.Gain).SequenceEqual(
            page.Targets.Select(t => t.Gain).OrderByDescending(g => g)));
    }

    [Fact]
    public async Task TheCarryoverShowsWhichPoolPhoenix2WouldGiveYou()
    {
        // Phoenix 2 pays a Singles chart one level up, so a pool of S22s and D23s
        // reorders. The pair of counts is the page's headline.
        var ctx = new PageContext()
            .WithPhoenixScores(ChartType.Single, 22, 30, 985_000)
            .WithPhoenixScores(ChartType.Double, 23, 30, 985_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(50, carry.SinglesInPool + carry.DoublesInPool);
        Assert.Equal(50, carry.Phoenix1SinglesInPool + carry.Phoenix1DoublesInPool);
        Assert.True(carry.Projected > 0);
    }

    [Fact]
    public async Task AChartWithNoPhoenix2AppearanceIsAFactNotATarget()
    {
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000, availableInPhoenix2: false);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.NotEmpty(carry.Unavailable);
        Assert.All(carry.Entries, e => Assert.False(e.AvailableInPhoenix2));
    }

    [Fact]
    public async Task TheCarryoverCountsWhatYouHaveNotScoredHereYet()
    {
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(0, carry.ScoredHere);
        Assert.Equal(50, carry.NotYetScored);
    }

    // ------------------------------------------------------------------ context

    private sealed class PageContext
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly List<Chart> _charts = new();
        private readonly Dictionary<Guid, RecordedPhoenixScore> _myBests = new();
        private readonly HashSet<Guid> _phoenix2Charts = new();
        private readonly Dictionary<Guid, RecordedPhoenixScore> _phoenix2Scores = new();
        private readonly Dictionary<Guid, PhoenixScore> _projected = new();
        private readonly Dictionary<Guid, int> _gains = new();
        private readonly List<RecordedPhoenixScore> _top = new();

        public PageContext()
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<IEnumerable<Chart>> request, CancellationToken _) =>
                {
                    var mix = ((GetChartsQuery)request).Mix;
                    return mix == MixEnum.Phoenix2
                        ? _charts.Where(c => _phoenix2Charts.Contains(c.Id)).ToArray()
                        : _charts.ToArray();
                });
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<IEnumerable<RecordedPhoenixScore>> request, CancellationToken _) =>
                {
                    var q = (GetTop50ForPlayerQuery)request;
                    return _top.Where(t => q.ChartType == null ||
                                           _charts.First(c => c.Id == t.ChartId).Type == q.ChartType);
                });
            Mediator.Setup(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new PumbilityProjection(_projected, _gains,
                    new Dictionary<Guid, TierListCategory>(), new Dictionary<Guid, ProjectionEvidence>()));

            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, Guid _, CancellationToken _) =>
                    mix == MixEnum.Phoenix2
                        ? _phoenix2Scores.Values.ToArray()
                        : _myBests.Values.ToArray());

            Saga = new PumbilityPageSaga(Mediator.Object, Scores.Object);
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public PumbilityPageSaga Saga { get; }

        private Chart AddChart(ChartType type, int level, bool inPhoenix2 = true)
        {
            var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
                new Song($"Song {_charts.Count}", SongType.Arcade, new Uri("https://piu.test/i.png"),
                    TimeSpan.FromMinutes(2), "Artist", 180),
                type, level, MixEnum.Phoenix, null, null, new HashSet<Skill>());
            _charts.Add(chart);
            if (inPhoenix2) _phoenix2Charts.Add(chart.Id);
            return chart;
        }

        /// <summary>A pool with descending values, so places and the bar are unambiguous.</summary>
        public PageContext WithPool(int count, ChartType type, int level)
        {
            for (var i = 0; i < count; i++)
            {
                var chart = AddChart(type, level);
                var score = new RecordedPhoenixScore(chart.Id, 995_000 - i * 500, PhoenixPlate.MarvelousGame,
                    false, Now.AddDays(-30));
                _top.Add(score);
                _myBests[chart.Id] = score;
            }

            return this;
        }

        public PageContext WithTarget(out Guid chartId, int gain, int projected, int? current = null)
        {
            var chart = AddChart(ChartType.Single, 21);
            chartId = chart.Id;
            _projected[chart.Id] = projected;
            _gains[chart.Id] = gain;
            if (current != null)
                _myBests[chart.Id] = new RecordedPhoenixScore(chart.Id, current.Value,
                    PhoenixPlate.TalentedGame, false, Now.AddDays(-200));
            return this;
        }

        public PageContext WithPhoenixScores(ChartType type, int level, int count, int score,
            bool availableInPhoenix2 = true)
        {
            for (var i = 0; i < count; i++)
            {
                var chart = AddChart(type, level, availableInPhoenix2);
                _myBests[chart.Id] = new RecordedPhoenixScore(chart.Id, score - i * 100,
                    PhoenixPlate.MarvelousGame, false, Now.AddDays(-60));
            }

            return this;
        }
    }
}
