using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed partial class PumbilityProjectionSagaTests
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
    public async Task Phoenix2HasNoLevelWindow()
    {
        // D24: a level-20 player is shown a 26 when five PUMBILITY peers have passed it — the
        // "D23 a level 18 can pass after memorizing one section" case, and the reason the
        // window went. Only the five-peer floor and the bar arithmetic decide.
        var ctx = new ProjectionContext(20).WithPhoenix2Pool(50, 17_500)
            .WithChart(out var far, ChartType.Single, 26, 26.0);
        for (var i = 0; i < 5; i++) ctx.WithPumbilityPeer(far, phoenix2Score: 900_000 + i * 1_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Contains(far.Id, result.ExpectedScores.Keys);
        // The default read is the first quartile (D50): on five voices at 900k..904k it sits
        // three-quarters of the way from the first to the second.
        Assert.Equal(900_750, (int)result.ExpectedScores[far.Id]);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnEnergyReadsAnotherRungOffTheSameSweep()
    {
        // D51: the sweep holds every rung the chip can ask for, so Good, Great and Top of my game
        // are three prices of one read — the band is drawn once, and the estimate moves from the
        // first quartile to the median to the third.
        var ctx = new ProjectionContext(20).WithPhoenix2Pool(50, 17_500)
            .WithChart(out var chart, ChartType.Single, 22);
        foreach (var score in new[] { 940_000, 962_000, 975_000, 985_000, 990_000 })
            ctx.WithPumbilityPeer(chart, phoenix2Score: score);

        var good = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);
        var great = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2, null, Energy.Great),
            CancellationToken.None);
        var top = await ctx.Saga.Handle(
            new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2, null, Energy.TopOfMyGame), CancellationToken.None);

        Assert.Equal(956_500, (int)good.ExpectedScores[chart.Id]);
        Assert.Equal(975_000, (int)great.ExpectedScores[chart.Id]);
        Assert.Equal(986_250, (int)top.ExpectedScores[chart.Id]);
        Assert.True(good.ProjectedGains[chart.Id] < great.ProjectedGains[chart.Id]);
        Assert.True(great.ProjectedGains[chart.Id] < top.ProjectedGains[chart.Id]);
        ctx.Stats.Verify(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, It.IsAny<ChartType>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheSweepNamesThePeerGroupPerTypeAndTheDarkOne()
    {
        // Fifty singles, twenty-nine doubles: singles peers are lit and counted, doubles say how
        // far the pool is from lighting up (D27, D28). Both ride the projection so the page can
        // print them without a second sweep.
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithChart(out var single, ChartType.Single, 20)
            .WithChart(out var dbl, ChartType.Double, 20);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(single, phoenix2Score: 970_000);
        ctx.WithPhoenix2DoublesPool(29);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var singles = result.Peers![ChartType.Single];
        Assert.Equal(PeerGroupKind.PumbilityPeers, singles.Kind);
        Assert.Equal(17_609.59, singles.Center); // the viewer's singles pool, off their stats row (D53)
        Assert.False(singles.PlacedByEstimate);
        Assert.Equal(6, singles.Size);
        Assert.True(singles.IsLit);
        var doubles = result.Peers[ChartType.Double];
        Assert.False(doubles.IsLit);
        Assert.Equal(29, doubles.PoolCount);
        Assert.Equal(50, doubles.PoolSize);
        Assert.DoesNotContain(dbl.Id, result.ExpectedScores.Keys);
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
        // Unrounded on both sides: the saga no longer truncates the gain, so neither does
        // the expectation. Compared to the cent, because two doubles that took different
        // routes to the same value are not bit-identical.
        var expected = scoring.GetScore(chart, projected,
                           ScoringConfiguration.ExpectedPlateForScore(projected), false)
                       - ctx.PoolBaseline(scoring);
        Assert.Equal(expected, result.ProjectedGains[chart.Id], 6);
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
        // Unrounded on both sides: the saga no longer truncates the gain, so neither does
        // the expectation. Compared to the cent, because two doubles that took different
        // routes to the same value are not bit-identical.
        var expected = scoring.GetScore(weak, projected,
                           ScoringConfiguration.ExpectedPlateForScore(projected), false)
                       - ctx.PoolBaseline(scoring);
        Assert.Equal(expected, result.ProjectedGains[weak.Id], 6);
    }

    [Fact]
    public async Task AChartThatCannotClearTheBarIsNotOffered()
    {
        // A weak 15 against a pool of 22s. Its value at a PERFECT game is still under the bar,
        // so it is dropped before anyone's scores are read — not estimated and then discarded.
        // That is the cheap half of the projection: the database is never asked about it.
        var ctx = new ProjectionContext(17).WithChart(out var weak, ChartType.Single, 15);
        ctx.WithPeerScores(weak, 910_000, 915_000, 920_000, 925_000);
        ctx.WithFullPoolAt(990_000, ChartType.Single, 22);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.DoesNotContain(weak.Id, result.ProjectedGains.Keys);
        Assert.DoesNotContain(weak.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task TheAdviceStopsAtAHundredAndKeepsTheBestOnes()
    {
        // A full window clears the bar on well over a thousand charts. Nobody plans past the
        // first hundred, so the tail is payload and scrolling for suggestions no one reads.
        // Flat rather than per type: the query itself is type-scoped by the pool selector.
        var ctx = new ProjectionContext();
        var offered = new List<int>();
        for (var i = 0; i < 130; i++)
        {
            ctx.WithChart(out var single, ChartType.Single, 20);
            ctx.WithPeerScores(single, 900_000 + i * 500, 905_000 + i * 500, 910_000 + i * 500);
            offered.Add(910_000 + i * 500);
        }

        var result = await ctx.Saga.Handle(
            new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix, ChartType.Single),
            CancellationToken.None);

        Assert.Equal(100, result.ProjectedGains.Count);
        // The best hundred, not an arbitrary hundred: the weakest survivor still out-gains
        // everything that was dropped.
        Assert.True(result.ProjectedGains.Values.Min() > 0);
        Assert.Equal(100, result.ExpectedScores.Count);
    }

    [Fact]
    public async Task ARepeatVisitDoesNotSweepTheCohortAgain()
    {
        // The cohort sweep and the history read are sized by the player population, not by
        // the viewer, so a second visit must not pay for them twice.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        // Once, not twice: the fixture holds singles only, so the doubles pass finds nothing
        // in scope and returns before it reads anything. The point is the SECOND visit adds
        // nothing at all.
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.History.Verify(h => h.GetHistory(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnImportDropsTheCachedProjection()
    {
        // Serving a projection that predates your own import reads as the page ignoring the
        // scores you just uploaded, which is worse than being slow.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Cache.Evict(ctx.UserId, MixEnum.Phoenix);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        // Twice over: the eviction sent the second visit back to the database, which is the
        // whole contract — an import must not be able to serve you a projection older than it.
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AWipeWithNoNamedMixDropsEveryMix()
    {
        // PlayerScoreDataDeletedEvent carries a null mix for an all-mixes wipe, and a
        // projection surviving that would keep recommending scores that no longer exist.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Cache.Evict(ctx.UserId, null);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OnePlayersImportLeavesAnotherPlayersProjectionAlone()
    {
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Cache.Evict(Guid.NewGuid(), MixEnum.Phoenix);
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TwoCallersArrivingTogetherShareOneSweep()
    {
        // The dashboard's suggestion widget and the page itself ask for the same thing
        // seconds apart, which is the design rather than an edge case. Caching the RESULT
        // would let the second arrival start its own sweep while the first was still
        // running; caching the task is what stops that.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);

        var gate = new TaskCompletionSource();
        var sweeps = 0;
        ctx.Scores.Setup(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref sweeps);
                await gate.Task;
                return Array.Empty<UserPhoenixScore>().AsEnumerable();
            });

        var first = ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);
        var second = ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, sweeps);
    }

    [Fact]
    public async Task AFailedSweepIsNotHandedToEverybodyForADay()
    {
        // A cached failure would outlive its cause by the whole lifetime, and nothing short
        // of a restart would clear it.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScores(chart, 950_000, 955_000, 960_000, 965_000);

        var attempts = 0;
        ctx.Scores.Setup(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("the ledger was busy");
                return Task.FromResult(Array.Empty<UserPhoenixScore>().AsEnumerable());
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None));
        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(2, attempts);
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

    [Fact]
    public async Task APhoenix2ProjectionNeverHearsPhoenix1Evidence()
    {
        // Phoenix 2 is its own world (D21): five Phoenix 1 scores on this chart from players
        // who are PUMBILITY peers here say nothing, and the chart is not projected.
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_500)
            .WithChart(out var chart, ChartType.Single, 20);
        for (var i = 0; i < 5; i++)
            ctx.WithPumbilityPeer(chart, phoenix1Score: 940_000 + i * 5_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.DoesNotContain(chart.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task APhoenix2ProjectionIsTheFirstQuartileOfFiveOrMorePumbilityPeers()
    {
        // D50: the default read is the peers' first quartile — on five equal voices, three-quarters
        // of the way from 940k to 962k — not their median.
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_500)
            .WithChart(out var chart, ChartType.Single, 20);
        foreach (var score in new[] { 940_000, 985_000, 962_000, 990_000, 975_000 })
            ctx.WithPumbilityPeer(chart, phoenix2Score: score);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Equal(956_500, (int)result.ExpectedScores[chart.Id]);
    }

    [Fact]
    public async Task FourPumbilityPeersAreNotAnOpinionWhileTheBandAnswersElsewhere()
    {
        // The floor still stands per chart. A band that answered anywhere is a band that could
        // answer, so the four-peer chart beside the five-peer one stays absent — the D47 fallback
        // is a rescue from an empty run, not a lower bar.
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_500)
            .WithChart(out var thin, ChartType.Single, 20)
            .WithChart(out var answered, ChartType.Single, 20);
        var peers = new List<Guid>();
        foreach (var score in new[] { 985_000, 985_000, 990_000, 990_000 })
        {
            ctx.WithPumbilityPeer(out var peer, thin, phoenix2Score: score);
            peers.Add(peer);
        }

        ctx.WithPumbilityPeer(out var fifth, answered, phoenix2Score: 975_000);
        foreach (var peer in peers) ctx.WithPeerPhoenix2Score(peer, answered, 975_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.NotNull(fifth);
        Assert.Contains(answered.Id, result.ExpectedScores.Keys);
        Assert.DoesNotContain(thin.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task ABandTooThinToMeetTheFloorGetsAnAnswerRatherThanAnEmptyPage()
    {
        // Four peers can never put five voices on anything, so the floor takes the whole run
        // rather than filtering it. The page suggests charts, and one peer's score is a worse
        // suggestion than five but a better one than an empty board (D47).
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_500)
            .WithChart(out var chart, ChartType.Single, 20);
        foreach (var score in new[] { 985_000, 985_000, 990_000, 990_000 })
            ctx.WithPumbilityPeer(chart, phoenix2Score: score);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Contains(chart.Id, result.ExpectedScores.Keys);
        // The group says the run relaxed, which is what the page's thin-band note reads (D47).
        Assert.True(result.Peers![ChartType.Single].AnsweredBelowFloor);
    }

    [Fact]
    public async Task APhoenix1ProjectionNeverReachesIntoPhoenix2()
    {
        // The reference mix runs one way only. Phoenix 1 is the populated mix; borrowing
        // back from the launch mix would add nothing and would make the older page's
        // numbers move when the newer one gains scores.
        var ctx = new ProjectionContext().WithChart(out var chart, ChartType.Single, 20);
        ctx.WithPeerScore(chart, 950_000, mix: MixEnum.Phoenix2)
            .WithPeerScore(chart, 955_000, mix: MixEnum.Phoenix2)
            .WithPeerScore(chart, 960_000, mix: MixEnum.Phoenix2);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.DoesNotContain(chart.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task AShortPoolIsPlacedByItsExtrapolatedFinishAndSaysSo()
    {
        // Twenty-five singles: too few for a settled pool, enough to extrapolate one. The pool's
        // own sum would seat this player among the weakest; filling the empty slots at the standard
        // they already hold draws their peers around where the pool is actually heading (D48, D53).
        // Every chart is priced the same here, so the twenty-five they hold and the twenty-five
        // they do not come to fifty of one value.
        var ctx = new ProjectionContext().WithPhoenix2Pool(25, 0)
            .WithPoolOf(25, 970_000, ChartType.Single, 20)
            .WithChart(out var chart, ChartType.Single, 20);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(chart, phoenix2Score: 985_000);
        var expected = ProjectionContext.PricedAt(ctx.ChartsInPool.First(), 970_000) * PeerGroup.PumbilityPoolSize;

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var group = result.Peers![ChartType.Single];
        Assert.True(group.IsLit);
        Assert.True(group.PlacedByEstimate);
        Assert.Equal(expected, group.Center, 6);
        Assert.Equal(PeerGroup.PumbilityProjectionGate, group.PoolSize);
        Assert.Equal(25, group.PoolCount);
        Assert.Contains(chart.Id, result.ExpectedScores.Keys);
    }

    [Fact]
    public async Task AnUnevenShortPoolFillsItsEmptySlotsAtTheWeakestChartHeld()
    {
        // The case a uniform pool cannot tell apart. Twenty strong charts and five weak ones: an
        // average over the top twenty would price all twenty-five empty slots at the STRONG value
        // and read the player high, which is what it did for all 111 backtested accounts. What
        // they hold counts for what it is worth, and only the twenty-five slots they do not hold
        // are guessed — at the weakest chart in the pool, the standard they are already holding
        // at its bottom.
        var ctx = new ProjectionContext().WithPhoenix2Pool(25, 0)
            .WithPoolOf(20, 995_000, ChartType.Single, 23)
            .WithPoolOf(5, 850_000, ChartType.Single, 11)
            .WithChart(out var chart, ChartType.Single, 20);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(chart, phoenix2Score: 985_000);

        var strong = ProjectionContext.PricedAt(ctx.ChartsInPool[0], 995_000);
        var weak = ProjectionContext.PricedAt(ctx.ChartsInPool[20], 850_000);
        // Held: 20 strong + 5 weak. Empty: 25 more at the weak value, the pool's floor.
        var expected = 20 * strong + 30 * weak;
        // The test is only worth anything if the old estimator would have answered differently.
        Assert.NotEqual(expected, strong * PeerGroup.PumbilityPoolSize);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var group = result.Peers![ChartType.Single];
        Assert.True(group.PlacedByEstimate);
        Assert.Equal(expected, group.Center, 6);
    }

    [Fact]
    public async Task AShortPoolOfOneTypeIsPlacedByItsOwnFinishWhateverTheOtherTypeHolds()
    {
        // Fifty singles and twenty-nine doubles. The doubles peers are drawn around the DOUBLES
        // pool (D53), and twenty-nine charts is not a settled one: the doubles group lights on the
        // shorter gate, placed by where the doubles pool would finish, and says so — the full
        // singles pool has nothing to say about it. The singles pool sits at level 15 so the merged
        // bar is comfortably below what a level-20 doubles chart is worth — the doubles row has to
        // clear it to reach ExpectedScores at all.
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithFullPoolAt(900_000, ChartType.Single, 15)
            .WithPoolOf(29, 960_000, ChartType.Double, 18)
            .WithChart(out var dbl, ChartType.Double, 20);
        ctx.WithPhoenix2DoublesPool(29);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(dbl, phoenix2Score: 985_000);
        var held = ctx.ChartsInPool.First(c => c.Type == ChartType.Double && (int)c.Level == 18);
        var expected = ProjectionContext.PricedAt(held, 960_000) * PeerGroup.PumbilityPoolSize;

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var doubles = result.Peers![ChartType.Double];
        Assert.True(doubles.IsLit);
        Assert.True(doubles.PlacedByEstimate);
        Assert.Equal(expected, doubles.Center, 6);
        Assert.Equal(29, doubles.PoolCount);
        Assert.Equal(PeerGroup.PumbilityProjectionGate, doubles.PoolSize);
        Assert.Contains(dbl.Id, result.ExpectedScores.Keys);
        // The singles group is placed by its settled pool and claims no estimate.
        Assert.False(result.Peers[ChartType.Single].PlacedByEstimate);
    }

    [Fact]
    public async Task UnderTheGateTheChipCountsTowardTwentyRatherThanFifty()
    {
        // Nineteen charts and dark. The threshold quoted has to be the one that will actually
        // light this player up, not a fifty they never have to reach.
        var ctx = new ProjectionContext().WithPhoenix2Pool(19, 0)
            .WithPoolOf(19, 970_000, ChartType.Single, 20)
            .WithChart(out var chart, ChartType.Single, 20);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(chart, phoenix2Score: 985_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        var group = result.Peers![ChartType.Single];
        Assert.False(group.IsLit);
        Assert.Equal(19, group.PoolCount);
        Assert.Equal(PeerGroup.PumbilityProjectionGate, group.PoolSize);
        Assert.Empty(result.ExpectedScores);
    }

    [Fact]
    public async Task AnAccountWithoutAFullPhoenix2PoolOfTheTypeGetsNothingForThatType()
    {
        // D28: the peers exist, the viewer is not yet in a position to have any. Twenty-nine
        // doubles is not a doubles pool, so the doubles chart is not projected however many
        // PUMBILITY peers have played it.
        var ctx = new ProjectionContext().WithPhoenix2Pool(29, 17_500)
            .WithChart(out var chart, ChartType.Double, 20);
        for (var i = 0; i < 6; i++) ctx.WithPumbilityPeer(chart, phoenix2Score: 970_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.DoesNotContain(chart.Id, result.ExpectedScores.Keys);
    }

    private sealed class ProjectionContext
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly List<Chart> _charts = new();
        private readonly double _doubles;
        private readonly Dictionary<Guid, HashSet<MixEnum>> _peerCohortMixes = new();
        private readonly List<PlayerRatingRecord> _peerHistory = new();
        private readonly Dictionary<Guid, double> _peerLevelNow = new();
        private readonly List<(MixEnum Mix, UserPhoenixScore Score)> _peerScores = new();
        private readonly Dictionary<Guid, double> _scoringLevels = new();
        private readonly double _singles;
        private readonly List<RecordedPhoenixScore> _topScores = new();
        // Phoenix 2: the viewer's total pool (their rung) and the size of their pool of each type,
        // and each PUMBILITY peer's pool size — pools are counted from the score read, so the
        // fixture answers that read with as many distinct filler charts as the size says.
        private double _phoenix2Total;
        private readonly Dictionary<ChartType, int> _phoenix2PoolSizes = new();
        private readonly Dictionary<Guid, int> _pumbilityPeerPools = new();
        private readonly Dictionary<Guid, double> _peerTotals = new();
        private readonly Dictionary<Guid, (string Name, bool IsPublic)> _peerIdentity = new();

        public ProjectionContext(double singlesCompetitive = 20, double doublesCompetitive = 20)
        {
            _singles = singlesCompetitive;
            _doubles = doublesCompetitive;

            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.Is<Guid>(g => g == UserId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, Guid _, CancellationToken _) =>
                    StatsFor(UserId, _singles, _doubles, mix == MixEnum.Phoenix2 ? _phoenix2Total : 0));
            Stats.Setup(s => s.GetPlayersByPoolOfType(MixEnum.Phoenix2, It.IsAny<ChartType>(), It.IsAny<double>(), It.IsAny<double>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _pumbilityPeerPools.Keys.ToArray().AsEnumerable());
            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, IEnumerable<Guid> ids, CancellationToken _) => ids
                    .Where(id => id == UserId || KnownIn(id, mix))
                    .Select(id => id == UserId
                        ? StatsFor(UserId, _singles, _doubles, mix == MixEnum.Phoenix2 ? _phoenix2Total : 0)
                        : StatsFor(id, _peerLevelNow[id], _peerLevelNow[id], _peerTotals.GetValueOrDefault(id)))
                    .ToArray().AsEnumerable());
            // The roster names peers through the user reader; the viewer's own scores come off
            // the best-score read, the same list the top-fifty query answers with.
            Users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids
                    .Where(id => id == UserId || _peerIdentity.ContainsKey(id))
                    .Select(id => id == UserId
                        ? new UserBuilder().WithId(UserId).WithName("Viewer").WithIsPublic(true).Build()
                        : new UserBuilder().WithId(id).WithName(_peerIdentity[id].Name).WithIsPublic(_peerIdentity[id].IsPublic).Build())
                    .ToArray().AsEnumerable());
            CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
            CurrentUser.Setup(c => c.User).Returns(() => new UserBuilder().WithId(UserId).WithName("Viewer").Build());
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.Is<Guid>(g => g == UserId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _topScores.ToArray().AsEnumerable());
            Stats.Setup(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, ChartType? _, double _, double _, CancellationToken _) =>
                    _peerLevelNow.Keys.Where(id => KnownIn(id, mix)).ToArray().AsEnumerable());

            History.Setup(h => h.GetHistory(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _peerHistory.ToArray().AsEnumerable());

            // The level-band read the saga actually uses: a range scan rather than several
            // hundred chart GUIDs in an IN list.
            Scores.Setup(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, IEnumerable<Guid> userIds, ChartType type, DifficultyLevel min,
                    DifficultyLevel max, CancellationToken _) =>
                {
                    var asked = userIds.ToHashSet();
                    var real = _peerScores
                        .Where(p => p.Mix == mix && asked.Contains(p.Score.UserId))
                        .Where(p =>
                        {
                            var chart = _charts.FirstOrDefault(c => c.Id == p.Score.ChartId);
                            return chart != null && chart.Type == type
                                                 && (int)chart.Level >= (int)min && (int)chart.Level <= (int)max;
                        })
                        .Select(p => p.Score).ToArray();
                    if (mix != MixEnum.Phoenix2) return real.AsEnumerable();
                    // Phoenix 2 counts pools from this read: pad each asked player's real scores of
                    // the type with distinct filler charts up to their declared pool size.
                    var fillers = asked.SelectMany(id => Enumerable.Range(0,
                            Math.Max(0, Phoenix2PoolSize(id, type) - real.Count(r => r.UserId == id)))
                        .Select(i => new UserPhoenixScore(id, FillerChart(id, i), "Peer", 950_000,
                            PhoenixPlate.FairGame, false)));
                    return real.Concat(fillers).ToArray().AsEnumerable();
                });

            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum mix, IEnumerable<Guid> userIds, IEnumerable<Guid> chartIds,
                    CancellationToken _) =>
                {
                    var wanted = chartIds.ToHashSet();
                    var asked = userIds.ToHashSet();
                    return _peerScores
                        .Where(p => p.Mix == mix && wanted.Contains(p.Score.ChartId) && asked.Contains(p.Score.UserId))
                        .Select(p => p.Score).ToArray().AsEnumerable();
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

            // A cache per context, so one test's projection can never answer another's.
            Cache = new PumbilityProjectionCache();
            // The real projector over the same stubbed ports rather than a double: these tests
            // are written against cohort membership and level-when-set, so faking it would leave
            // the plumbing this fixture exists to drive unexercised.
            Saga = new PumbilityProjectionSaga(Mediator.Object,
                new ScoreProjector(Scores.Object, Stats.Object, History.Object), Cache, Scores.Object, Stats.Object,
                Users.Object, CurrentUser.Object);
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerHistoryRepository> History { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public PumbilityProjectionSaga Saga { get; }

        public PumbilityProjectionCache Cache { get; }

        public ProjectionContext WithChart(out Chart chart, ChartType type, int level, double? scoringLevel = null)
        {
            chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
                new Song($"Song {_charts.Count}", SongType.Arcade, new Uri("https://piu.test/i.png"),
                    TimeSpan.FromMinutes(2), "Artist", 180),
                type, level, MixEnum.Phoenix, null, null);
            _charts.Add(chart);
            _scoringLevels[chart.Id] = scoringLevel ?? level;
            return this;
        }

        public ProjectionContext WithPeerScores(Chart chart, params int[] scores)
        {
            foreach (var score in scores) WithPeerScore(chart, score);
            return this;
        }

        public ProjectionContext WithPeerScore(Chart chart, int score, double levelsGrownSince = 0,
            MixEnum mix = MixEnum.Phoenix)
        {
            var peer = Guid.NewGuid();
            var levelNow = chart.Type == ChartType.Single ? _singles : _doubles;
            _peerLevelNow[peer] = levelNow;
            _peerCohortMixes[peer] = new HashSet<MixEnum> { mix };
            var recordedAt = Now.AddDays(-100);
            _peerScores.Add((mix, new UserPhoenixScore(peer, chart.Id, "Peer", score, PhoenixPlate.MarvelousGame,
                false, true, recordedAt)));
            // One history row dated before the score: the level they held when they set it.
            var then = levelNow - levelsGrownSince;
            _peerHistory.Add(new PlayerRatingRecord(peer, recordedAt.AddDays(-1), then, then, then, 0, 0));
            return this;
        }

        /// <summary>
        ///     The viewer's Phoenix 2 standing: a pool total, which the stats row reports for the
        ///     merged pool and for each type's, and a pool of <paramref name="poolSize" /> charts
        ///     of every type.
        /// </summary>
        public ProjectionContext WithPhoenix2Pool(int poolSize, double total)
        {
            _phoenix2Total = total;
            _phoenix2PoolSizes[ChartType.Single] = poolSize;
            _phoenix2PoolSizes[ChartType.Double] = poolSize;
            return this;
        }

        public ProjectionContext WithPhoenix2DoublesPool(int poolSize)
        {
            _phoenix2PoolSizes[ChartType.Double] = poolSize;
            return this;
        }

        /// <summary>
        ///     One PUMBILITY peer — inside the viewer's window with a full pool of every type —
        ///     holding a Phoenix 2 score and/or a Phoenix 1 score on <paramref name="chart" />.
        /// </summary>
        public ProjectionContext WithPumbilityPeer(Chart chart, int? phoenix2Score = null, int? phoenix1Score = null,
            int poolSize = 50)
        {
            return WithPumbilityPeer(out _, chart, phoenix2Score, phoenix1Score, poolSize);
        }

        /// <summary>
        ///     The same, handing the peer's id back so a test can score them on more charts, name
        ///     them, or hide them.
        /// </summary>
        public ProjectionContext WithPumbilityPeer(out Guid peer, Chart chart, int? phoenix2Score = null,
            int? phoenix1Score = null, int poolSize = 50, string? name = null, bool isPublic = true, double total = 17_500)
        {
            peer = Guid.NewGuid();
            _pumbilityPeerPools[peer] = poolSize;
            _peerLevelNow[peer] = 20;
            _peerTotals[peer] = total;
            _peerIdentity[peer] = (name ?? $"Peer {_peerIdentity.Count + 1}", isPublic);
            _peerCohortMixes[peer] = new HashSet<MixEnum> { MixEnum.Phoenix, MixEnum.Phoenix2 };
            if (phoenix2Score is { } p2)
                _peerScores.Add((MixEnum.Phoenix2, new UserPhoenixScore(peer, chart.Id, "Peer", p2,
                    PhoenixPlate.MarvelousGame, false, true, Now.AddDays(-10))));
            if (phoenix1Score is { } p1)
                _peerScores.Add((MixEnum.Phoenix, new UserPhoenixScore(peer, chart.Id, "Peer", p1,
                    PhoenixPlate.MarvelousGame, false, true, Now.AddDays(-400))));
            return this;
        }

        /// <summary>A Phoenix 2 score for a peer this fixture already knows, on another chart.</summary>
        public ProjectionContext WithPeerPhoenix2Score(Guid peer, Chart chart, int score)
        {
            _peerScores.Add((MixEnum.Phoenix2, new UserPhoenixScore(peer, chart.Id, "Peer", score,
                PhoenixPlate.MarvelousGame, false, true, Now.AddDays(-10))));
            return this;
        }

        /// <summary>A Phoenix 1 score for a peer this fixture already knows, on another chart.</summary>
        public ProjectionContext WithPeerPhoenix1Score(Guid peer, Chart chart, int score)
        {
            _peerScores.Add((MixEnum.Phoenix, new UserPhoenixScore(peer, chart.Id, "Peer", score,
                PhoenixPlate.MarvelousGame, false, true, Now.AddDays(-400))));
            return this;
        }

        /// <summary>One of the viewer's own scores, as the best-score read and the top-fifty query both see it.</summary>
        public ProjectionContext WithOwnScore(Chart chart, int score, PhoenixPlate plate = PhoenixPlate.MarvelousGame)
        {
            _topScores.Add(new RecordedPhoenixScore(chart.Id, score, plate, false, Now.AddDays(-30)));
            return this;
        }

        private int Phoenix2PoolSize(Guid id, ChartType type)
        {
            if (id == UserId) return _phoenix2PoolSizes.GetValueOrDefault(type);
            return _pumbilityPeerPools.GetValueOrDefault(id);
        }

        private static Guid FillerChart(Guid user, int index)
        {
            var bytes = user.ToByteArray();
            bytes[0] = (byte)(index & 0xFF);
            bytes[1] = (byte)((index >> 8) & 0xFF);
            bytes[2] ^= 0x5A;
            return new Guid(bytes);
        }

        public ChartType TypeOf(Guid chartId) => _charts.First(c => c.Id == chartId).Type;

        private bool KnownIn(Guid peer, MixEnum mix)
        {
            return _peerCohortMixes.TryGetValue(peer, out var mixes) && mixes.Contains(mix);
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
            return WithPoolOf(50, score, type, level);
        }

        /// <summary>
        ///     A pool of exactly <paramref name="count" /> charts, all priced the same. What
        ///     GetTop50ForPlayerQuery returns, which is what the projected finish is read from —
        ///     so a test can put the merged pool either side of fifty and of the projection gate.
        /// </summary>
        public ProjectionContext WithPoolOf(int count, int score, ChartType type, int level)
        {
            for (var i = 0; i < count; i++)
            {
                WithChart(out var filler, type, level);
                _topScores.Add(new RecordedPhoenixScore(filler.Id, score, PhoenixPlate.MarvelousGame, false,
                    Now.AddDays(-200)));
            }

            return this;
        }

        /// <summary>The charts this fixture has made, so a test can price one the way the pool was.</summary>
        public IReadOnlyList<Chart> ChartsInPool => _charts;

        /// <summary>What one chart of the pool above is worth, for a test that predicts the finish.</summary>
        public static double PricedAt(Chart chart, int score)
        {
            return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
                .GetScore(chart, score, PhoenixPlate.MarvelousGame, false);
        }

        public double PoolBaseline(ScoringConfiguration scoring)
        {
            if (_topScores.Count < 50) return 0;
            return _topScores
                .Select(t => (int)scoring.GetScore(_charts.First(c => c.Id == t.ChartId), t.Score!.Value,
                    t.Plate ?? PhoenixPlate.RoughGame, t.IsBroken))
                .OrderByDescending(v => v).Take(50).Min();
        }

        /// <summary>
        ///     A stats row: the competitive levels, and <paramref name="total" /> standing for the
        ///     merged pool AND the pool of each type — the fixture declares one pool for every
        ///     type (WithPhoenix2Pool), so the window a type's peers are drawn around is that (D53).
        /// </summary>
        private static PlayerStatsRecord StatsFor(Guid userId, double singles, double doubles, double total = 0)
        {
            return new PlayerStatsRecord(userId, 0, 1, 0, 0, 0,
                total, 0, 0,
                total, 0, 0,
                total, 0, 0,
                (singles + doubles) / 2,
                singles,
                doubles);
        }
    }
}
