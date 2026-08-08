using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
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
    public async Task InPhoenix2YourOwnPhoenix1ScoresBecomeTargets()
    {
        // The point of the feature: charts from your Phoenix 1 pool you have not scored here
        // are the plan, and they need no cohort to produce.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotEmpty(page.Targets);
        Assert.All(page.Targets, t => Assert.Equal(TargetSource.Phoenix1, t.Source));
    }

    [Fact]
    public async Task AScoreYouAlreadyHitBeatsAnEstimateOfWhatYouMight()
    {
        // Both sources have an opinion on the same chart. There is no better evidence than a
        // score already on record, so the estimate must not survive.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);
        var contested = ctx.FirstPhoenixChart();
        ctx.WithPeerProjection(contested, projected: 910_000, gain: 999);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var row = page.Targets.Single(t => t.ChartId == contested);
        Assert.Equal(TargetSource.Phoenix1, row.Source);
        Assert.Equal(985_000, (int)row.Projected);
    }

    [Fact]
    public async Task CarryoverSuggestionsReachPastTheFiftiethScore()
    {
        // The gap this closes: capping suggestions at the pool hid rows carrying the best
        // evidence there is. Against an empty Phoenix 2 pool the bar is zero, so a repriced
        // #73 clears it as surely as a #3 does — and it is still a score actually hit.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 120, 985_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.True(page.Targets.Count > 50,
            $"only {page.Targets.Count} suggestions — the pool cap is still in the way");
        Assert.All(page.Targets, t => Assert.Equal(TargetSource.Phoenix1, t.Source));
    }

    [Fact]
    public async Task ThePoolFiguresStillMeanTheTopFiftyAfterReadingDeeper()
    {
        // Reading past the fiftieth is for suggestions only. PUMBILITY IS the top fifty, so
        // the panel's projected total and bar must not move when the candidate list grows.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 120, 985_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId),
            CancellationToken.None);

        Assert.Equal(50, carry.Entries.Count);
        Assert.Equal(50, carry.SinglesInPool + carry.DoublesInPool);
        Assert.NotEmpty(carry.Candidates);
        // Candidates rank behind the pool and say so.
        Assert.All(carry.Candidates, c => Assert.True(c.Place > 50));
    }

    [Fact]
    public async Task TheListIsOneRankedHundredAcrossBothSources()
    {
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 160, 985_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Equal(100, page.Targets.Count);
        Assert.True(page.Targets.Select(t => t.Gain).SequenceEqual(
            page.Targets.Select(t => t.Gain).OrderByDescending(g => g)));
    }

    [Fact]
    public async Task AChartBothSourcesNameSpendsOneSlotNotTwo()
    {
        // The merge dedups by chart before the cut. A duplicate would eat one of the hundred
        // and push a real suggestion off the end.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);
        var contested = ctx.FirstPhoenixChart();
        ctx.WithPeerProjection(contested, projected: 910_000, gain: 999);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Single(page.Targets, t => t.ChartId == contested);
    }

    [Fact]
    public async Task OnPhoenix1NothingClaimsToBeCarriedOver()
    {
        var ctx = new PageContext().WithPool(55, ChartType.Single, 20);
        ctx.WithTarget(out _, gain: 400, projected: 985_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId), CancellationToken.None);

        Assert.All(page.Targets, t => Assert.Equal(TargetSource.Peers, t.Source));
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
    public async Task AChartWithNoPhoenix2AppearanceIsNeverATarget()
    {
        // You cannot go and play it. It used to be counted in a fact tile as well, which the
        // owner cut for saying something nobody can act on — so the only claim left is this one.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000, availableInPhoenix2: false);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);
        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.All(carry.Entries, e => Assert.False(e.AvailableInPhoenix2));
        Assert.Empty(page.Targets);
    }

    [Fact]
    public async Task TheCarryoverSaysWhereTheRecordWouldLandOnAllThreeLadders()
    {
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 24, 55, 995_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        // Always all three, whatever pool was asked for: the argument is what the whole record
        // is worth here, and at a launch this is the only gem a player sees beside their name.
        Assert.Equal(3, carry.ProjectedTitles.Count);
        var singles = carry.ProjectedTitles.Single(t => t.Pool == PumbilityPool.Singles);
        Assert.Equal(carry.Projected, singles.Value, 2);
        Assert.NotNull(singles.Title);
        // The doubles pool is empty, so it lands nowhere rather than inventing a rung.
        Assert.Null(carry.ProjectedTitles.Single(t => t.Pool == PumbilityPool.Doubles).Title);
    }

    [Fact]
    public async Task AChartYouDidBetterOnInPhoenix1IsATargetPricedAtTheDifference()
    {
        // 985k there against 900k here is a gain worth playing for. It used to be dropped
        // outright for the sole reason that the chart already carried a Phoenix 2 score.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);
        var contested = ctx.FirstPhoenixChart();
        ctx.WithPhoenix2Score(contested, 900_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var row = page.Targets.Single(t => t.ChartId == contested);
        Assert.Equal(TargetSource.Phoenix1, row.Source);
        Assert.True(row.Gain > 0);
        // It holds the best Phoenix 1 score of the set, so nothing but the discount for what
        // the chart is already worth could put another chart's gain above it.
        Assert.True(row.Gain < page.Targets.Max(t => t.Gain),
            "the gain was not reduced by what the chart already contributes");
    }

    [Fact]
    public async Task ABrokenRunHereDoesNotCountAsHavingScoredTheChart()
    {
        // A stage break rates zero, so it displaces nothing — and a chart you broke on is
        // precisely the one your Phoenix 1 record has something to say about.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);
        var broke = ctx.FirstPhoenixChart();
        ctx.WithPhoenix2Score(broke, 900_000, true);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);
        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId),
            CancellationToken.None);

        // Undiscounted, so the best Phoenix 1 score of the set still carries the best gain.
        Assert.Equal(page.Targets.Max(t => t.Gain), page.Targets.Single(t => t.ChartId == broke).Gain);
        Assert.Equal(0, carry.ScoredHere);
    }

    [Fact]
    public async Task TheCarryoverCountsWhatYouHaveNotScoredHereYet()
    {
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 22, 55, 985_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(0, carry.ScoredHere);
        Assert.Equal(50, carry.NotYetScored);
    }

    [Fact]
    public async Task PhoenixHasNoTitleRailsBecauseItHasNoPumbilityLadders()
    {
        // Phoenix2PumbilityTitle is the only PUMBILITY-threshold title that exists, so there is
        // nothing to draw against on Phoenix — and no per-type split either.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Empty(page.Rails!);
        Assert.Null(page.Totals);
    }

    [Fact]
    public async Task TheAskIsTheNextRungSpreadOverAFullPool()
    {
        // The device the whole section turns on: a pool is fifty charts, so a threshold is a
        // flat per-chart value. It is true however the player gets there, which is why it
        // replaced counting charts.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var rail = page.Rails!.Single(r => r.Pool == PumbilityPool.Singles);
        Assert.NotNull(rail.NextThreshold);
        Assert.Equal(rail.NextThreshold!.Value / 50.0, rail.Ask, 6);
        Assert.Equal(rail.Value / 50.0, rail.Average, 6);
        Assert.Equal(Math.Max(0, rail.Ask - rail.Average), rail.PerChartGap, 6);
        Assert.InRange(rail.Progress, 0, 1);
    }

    [Fact]
    public async Task EveryLadderGetsARailWhicheverPoolIsSelected()
    {
        // Three goals held at once. Hiding two behind the pool selector would be worse than
        // showing all three, so the rails ignore it.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(
            new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Double),
            CancellationToken.None);

        Assert.Equal(3, page.Rails!.Count);
        Assert.Contains(page.Rails, r => r.Pool == PumbilityPool.Total);
        Assert.Contains(page.Rails, r => r.Pool == PumbilityPool.Singles);
        Assert.Contains(page.Rails, r => r.Pool == PumbilityPool.Doubles);
        // The doubles pool is empty here, so its rail is the not-started shape rather than absent.
        Assert.Equal(0, page.Rails.Single(r => r.Pool == PumbilityPool.Doubles).Value);
        Assert.Null(page.Rails.Single(r => r.Pool == PumbilityPool.Doubles).Held);
    }

    [Fact]
    public async Task TheAskNamesAChartThatWouldMeetIt()
    {
        // A per-chart value is only useful once you can picture the chart, so the rail resolves
        // it to the easiest level that gets there and the grade it would take.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var rail = page.Rails!.Single(r => r.Pool == PumbilityPool.Singles);

        // Three references, best grade first, so the levels ascend and the low one is the hard
        // one. A is the floor because it is the lowest multiplier this site has verified.
        Assert.Equal(new[] { PhoenixLetterGrade.SSSPlus, PhoenixLetterGrade.AAA, PhoenixLetterGrade.A },
            rail.Examples.Select(e => e.Grade));
        Assert.True(rail.Examples.Select(e => (int)e.Level).SequenceEqual(
            rail.Examples.Select(e => (int)e.Level).OrderBy(l => l)));

        // Every one has to actually meet the ask, at the grade named and with no plate help.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        Assert.All(rail.Examples, e => Assert.True(scoring.GetScore(e.Type, e.Level,
            e.Grade.GetMinimumScoreFor(MixEnum.Phoenix2), PhoenixPlate.RoughGame) >= rail.Ask));
    }

    [Fact]
    public async Task TheProjectedPoolSaysWhetherTheSuggestionsCanReachTheRung()
    {
        // The realism check: what the fifty would average if every suggestion landed. Read
        // against the ask, it says whether the list on Play gets there at all.
        //
        // A Phoenix 1 record with nothing scored in Phoenix 2 yet is the sharp case — the pool is
        // empty, every carried score is a suggestion, and the projection is therefore entirely
        // what the list would build.
        var ctx = new PageContext().WithPhoenixScores(ChartType.Single, 24, 55, 995_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var rail = page.Rails!.Single(r => r.Pool == PumbilityPool.Singles);
        Assert.Equal(0, rail.Average);
        Assert.NotNull(rail.ProjectedAverage);
        Assert.True(rail.ProjectedAverage!.Value > rail.Average,
            "the suggestions should build a pool better than the empty one they start from");
        Assert.Equal(rail.ProjectedAverage.Value >= rail.Ask, rail.ProjectionReaches);
    }

    [Fact]
    public async Task ThePoolSelectorTotalsComeBackOnTheRecord()
    {
        // Filling the selector used to mean running this whole read twice more from the page,
        // for two pools it was not looking at.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotNull(page.Totals);
        Assert.Equal(page.Total, page.Totals!.All);
        Assert.Equal(page.Total, page.Totals.Singles);
        Assert.Equal(0, page.Totals.Doubles);
    }

    [Fact]
    public async Task OnPhoenixTheBreakdownAttributesNothingToPlates()
    {
        // The whole reason the band exists: Phoenix 1's plate modifiers are all 1.0, so there
        // is no plate term to attribute and no ceiling to chase.
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.NotNull(page.Breakdown);
        Assert.Equal(0, page.Breakdown!.FromPlate);
        Assert.Equal(0, page.Breakdown.PlateHeadroom);
        Assert.False(page.Breakdown.PlatesCount);
    }

    [Fact]
    public async Task TheBreakdownIsTheTotalItSplitsAndTheChartsAreMostOfIt()
    {
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotNull(page.Breakdown);
        // The pool total truncates each entry to an int, so the split — which does not — sits
        // between it and one unit per chart above it.
        Assert.InRange(page.Breakdown!.Total, page.Total, page.Total + page.Pool.Count);
        Assert.True(page.Breakdown.Level > page.Breakdown.FromScore,
            "the charts should be the larger share of a pool");
        Assert.True(page.Breakdown.FromScore > page.Breakdown.FromPlate,
            "grades should outweigh plates by a wide margin");
    }

    [Fact]
    public async Task ThePlateCeilingIsWhatPerfectPlatesOnEveryChartWouldAdd()
    {
        var ctx = new PageContext().WithPool(50, ChartType.Single, 21);

        var page = await ctx.Saga.Handle(new GetPumbilityPageQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        // Under a fiftieth of the pool, which is the number the argument turns on.
        Assert.True(page.Breakdown!.PlateHeadroom > 0);
        Assert.InRange(page.Breakdown.ShareOf(page.Breakdown.PlateHeadroom), 0, 0.02);
        Assert.Equal(page.Breakdown.FromPlate + page.Breakdown.PlateHeadroom, page.Breakdown.PlateSpan, 6);
    }

    [Fact]
    public async Task AChartWorthNothingTakesNoSlotInTheRepricedPool()
    {
        // Forty-five charts that pay and ten that cannot: below level 10 the base rating is
        // zero in both mixes, so a perfect run on one is worth nothing at all. Without the
        // filter they fill the pool out to fifty, and every figure the panel prints off it —
        // the bar it would set, the singles/doubles split, how many are not yet scored — is
        // counted against charts that contribute nothing.
        var ctx = new PageContext()
            .WithPhoenixScores(ChartType.Single, 22, 45, 985_000)
            .WithPhoenixScores(ChartType.Single, 9, 10, 1_000_000);

        var carry = await ctx.Saga.Handle(new ProjectPhoenix2CarryoverQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(45, carry.Entries.Count);
        Assert.Equal(45, carry.SinglesInPool);
        Assert.Equal(45, carry.Phoenix1SinglesInPool);
        Assert.All(carry.Entries, e => Assert.True(e.Phoenix2Value > 0));
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
                    new Dictionary<Guid, TierListCategory>()));

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

        /// <summary>The first Phoenix 1 chart seeded — something for both sources to contest.</summary>
        public Guid FirstPhoenixChart() => _myBests.Keys.First();

        /// <summary>What the player holds in Phoenix 2 on a chart they also played in Phoenix 1.</summary>
        public PageContext WithPhoenix2Score(Guid chartId, int score, bool isBroken = false)
        {
            _phoenix2Scores[chartId] = new RecordedPhoenixScore(chartId, score, PhoenixPlate.TalentedGame,
                isBroken, Now.AddDays(-10));
            return this;
        }

        /// <summary>Gives the peer estimator an opinion on a chart, so precedence is testable.</summary>
        public PageContext WithPeerProjection(Guid chartId, int projected, int gain)
        {
            _projected[chartId] = projected;
            _gains[chartId] = gain;
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
