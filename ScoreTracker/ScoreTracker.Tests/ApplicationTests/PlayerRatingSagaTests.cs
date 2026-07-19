using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerRatingSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UserCreatedSavesZeroedStatsRecord()
    {
        var stats = new Mock<IPlayerStatsRepository>();
        var saga = BuildSaga(stats: stats);
        var userId = Guid.NewGuid();

        await saga.Consume(BuildContext(new UserCreatedEvent(userId)));

        stats.Verify(s => s.SaveStats(MixEnum.Phoenix, userId,
            It.Is<PlayerStatsRecord>(p => p.UserId == userId && p.TotalRating == 0
                                          && p.ClearCount == 0 && p.HighestLevel == DifficultyLevel.From(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTop50ForPlayerExcludesCoOpCharts()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var charts = ChartsMockReturning(new[] { single, coOp });
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(single.Id, 950000),
            Score(coOp.Id, 990000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType: null),
            CancellationToken.None)).ToArray();

        Assert.Single(result);
        Assert.Equal(single.Id, result[0].ChartId);
    }

    [Fact]
    public async Task GetTop50ForPlayerFiltersByChartTypeWhenSpecified()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var dbl = new ChartBuilder().WithType(ChartType.Double).WithLevel(17).Build();
        var charts = ChartsMockReturning(new[] { single, dbl });
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(single.Id, 950000),
            Score(dbl.Id, 950000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType.Double),
            CancellationToken.None)).ToArray();

        Assert.Single(result);
        Assert.Equal(dbl.Id, result[0].ChartId);
    }

    [Fact]
    public async Task GetTop50ForPlayerRespectsCountLimitAndOrdersByRatingDescending()
    {
        var c1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c3 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var charts = ChartsMockReturning(new[] { c1, c2, c3 });
        // Higher score = higher Pumbility rating for same chart type/level.
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(c1.Id, 800000),
            Score(c2.Id, 990000),
            Score(c3.Id, 900000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType: null, Count: 2),
            CancellationToken.None)).ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal(c2.Id, result[0].ChartId); // 990000 first
        Assert.Equal(c3.Id, result[1].ChartId); // 900000 second
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData(ChartType.Single, 50)]
    [InlineData(ChartType.Double, 50)]
    public async Task GetTop50CompetitiveTakesOneHundredForAllAndFiftyForFilteredType(
        ChartType? requestType, int expectedTakeCount)
    {
        // Build 120 charts so the take limit is observable for both 100 (no filter) and 50 (filtered).
        var charts = Enumerable.Range(0, 120)
            .Select(i => new ChartBuilder().WithType(i % 2 == 0 ? ChartType.Single : ChartType.Double)
                .WithLevel(15 + (i % 5)).Build())
            .ToArray();
        var chartsMock = ChartsMockReturning(charts);
        var scores = ScoresMockReturning(Guid.NewGuid(),
            charts.Select((c, i) => Score(c.Id, 800000 + i * 100)).ToArray());
        var saga = BuildSaga(charts: chartsMock, scores: scores);

        var result = (await saga.Handle(
            new GetTop50CompetitiveQuery(Guid.NewGuid(), requestType),
            CancellationToken.None)).ToArray();

        var matching = requestType == null
            ? charts.Length
            : charts.Count(c => c.Type == requestType);
        Assert.Equal(Math.Min(expectedTakeCount, matching), result.Length);
    }

    [Fact]
    public async Task RecalculateStatsCommandSavesNewStatsAndAlwaysPublishesStatsUpdatedEvent()
    {
        var userId = Guid.NewGuid();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
        var bus = new Mock<IBus>();
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(charts: ChartsMockReturning(Array.Empty<Chart>()),
            scores: ScoresMockReturning(userId, Array.Empty<RecordedPhoenixScore>()),
            stats: stats, bus: bus, mediator: mediator);

        await saga.Handle(new RecalculateStatsCommand(userId), CancellationToken.None);

        stats.Verify(s => s.SaveStats(MixEnum.Phoenix, userId, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(
            It.Is<PlayerStatsUpdatedEvent>(e => e.UserId == userId && e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(
            It.Is<PlayerStatsUpdatedEvent>(e => e.UserId == userId && e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculateStatsCommandPublishesRatingsImprovedWhenSkillRatingIncreases()
    {
        var userId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
        var bus = new Mock<IBus>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, bus: bus);

        await saga.Handle(new RecalculateStatsCommand(userId), CancellationToken.None);

        bus.Verify(b => b.Publish(It.Is<PlayerRatingsImprovedEvent>(e => e.UserId == userId
                                                                         && e.NewTop50 > e.OldTop50
                                                                         && e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompetitiveGainFlagsTheScoresThatDroveIt()
    {
        // Singles competitive improved and the changed single's Fung score meets the
        // old level -> it gets the CompetitiveImprover highlight in its session.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var highlights = new Mock<IScoreHighlightRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, highlights: highlights);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix, new[] { single.Id }, sessionId),
            CancellationToken.None);

        highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, userId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == single.Id && x.SessionId == sessionId
                && x.Flags == HighlightFlags.CompetitiveImprover)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RatingGainsBecomeSessionMilestones()
    {
        // Pumbility + Singles competitive gains capture; Doubles didn't gain and the
        // combined competitive is deliberately never a milestone.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var milestones = new Mock<IPlayerMilestoneRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, milestones: milestones);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix, new[] { single.Id }, sessionId),
            CancellationToken.None);

        milestones.Verify(m => m.Append(MixEnum.Phoenix, userId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.PumbilityGain && x.SessionId == sessionId)
                && w.Any(x => x.Kind == MilestoneKind.SinglesCompetitiveGain)
                && w.All(x => x.Kind != MilestoneKind.DoublesCompetitiveGain)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminRecalculationWithoutASessionWritesNoImproverFlags()
    {
        var userId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var highlights = new Mock<IScoreHighlightRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, highlights: highlights);

        await saga.Handle(new RecalculateStatsCommand(userId), CancellationToken.None);

        highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<ScoreHighlightWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecalculateStatsCommandDoesNotPublishRatingsImprovedWhenNothingImproves()
    {
        var userId = Guid.NewGuid();
        var stats = new Mock<IPlayerStatsRepository>();
        // Old stats already at high values — with no new scores, new stats are all 0,
        // so nothing exceeds old stats and no PlayerRatingsImprovedEvent should fire.
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(userId, TotalRating: 999999, HighestLevel: 25,
                ClearCount: 1000, CoOpRating: 999999, CoOpScore: 1000000, SkillRating: 999999, SkillScore: 1000000,
                SkillLevel: 25, SinglesRating: 999999, SinglesScore: 1000000, SinglesLevel: 25,
                DoublesRating: 999999, DoublesScore: 1000000, DoublesLevel: 25, CompetitiveLevel: 25,
                SinglesCompetitiveLevel: 25, DoublesCompetitiveLevel: 25));
        var bus = new Mock<IBus>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(Array.Empty<Chart>()),
            scores: ScoresMockReturning(userId, Array.Empty<RecordedPhoenixScore>()),
            stats: stats, bus: bus);

        await saga.Handle(new RecalculateStatsCommand(userId), CancellationToken.None);

        bus.Verify(b => b.Publish(It.IsAny<PlayerRatingsImprovedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecalculatePumbilityCommandUpdatesScoreStatsForGivenCharts()
    {
        var userId = Guid.NewGuid();
        var c1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(17).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(),
                It.Is<IEnumerable<Guid>>(ids => ids != null && ids.Contains(c1.Id) && ids.Contains(c2.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { c1, c2 });
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScores(
                MixEnum.Phoenix,
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)),
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(c1.Id) && ids.Contains(c2.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserPhoenixScore(userId, c1.Id, Name.From("alice"), 950000, PhoenixPlate.SuperbGame, false),
                new UserPhoenixScore(userId, c2.Id, Name.From("alice"), 900000, PhoenixPlate.MarvelousGame, false)
            });
        var recordStats = new Mock<IPhoenixRecordStatsRepository>();
        var saga = BuildSaga(charts: charts, scores: scores, recordStats: recordStats);

        await saga.Handle(
            new RecalculatePumbilityCommand(userId, new[] { c1.Id, c2.Id }),
            CancellationToken.None);

        recordStats.Verify(s => s.UpdateScoreStats(MixEnum.Phoenix, userId,
            It.Is<IEnumerable<PhoenixRecordStats>>(stats =>
                stats.Count() == 2 && stats.Any(p => p.ChartId == c1.Id) && stats.Any(p => p.ChartId == c2.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CaptureSessionStatsRecalculatesStatsAndPumbilityAndReturnsTheHarvest()
    {
        // The session-snapshot rating step: one dispatch recalculates stats + Pumbility
        // record stats and hands the minted milestones + improver charts back to the
        // orchestrator for the card.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var scores = ScoresMockReturning(userId, new[] { Score(single.Id, 950000) });
        scores.Setup(s => s.GetPlayerScores(
                MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        var recordStats = new Mock<IPhoenixRecordStatsRepository>();
        var saga = BuildSaga(charts: ChartsMockReturning(new[] { single }),
            scores: scores, stats: stats, recordStats: recordStats);

        var result = await saga.Handle(
            new PlayerRatingSaga.CaptureSessionStats(userId, MixEnum.Phoenix, new[] { single.Id }, sessionId),
            CancellationToken.None);

        stats.Verify(s => s.SaveStats(MixEnum.Phoenix, userId, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        recordStats.Verify(s => s.UpdateScoreStats(MixEnum.Phoenix, userId,
            It.IsAny<IEnumerable<PhoenixRecordStats>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(result.Milestones, m => m.Kind == MilestoneKind.PumbilityGain);
        Assert.Contains(single.Id, result.ImproverChartIds);
    }

    [Fact]
    public async Task CompetitiveMicroGainsUnderAHundredthAreNotMilestones()
    {
        // Revision-2 noise floor: the +0.002-style lines were the poster child of the
        // old message dump. (Pumbility still captures at any gain — even +1.)
        var userId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        var old = ZeroStats(userId) with
        {
            // Just under a hundredth below what one 950k S20 recomputes to.
            SinglesCompetitiveLevel = ScoringConfiguration.CalculateFungScore(single.Level, 950000) - 0.009,
            CompetitiveLevel = 0
        };
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(old);
        var milestones = new Mock<IPlayerMilestoneRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, milestones: milestones);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix, new[] { single.Id },
            Guid.NewGuid()), CancellationToken.None);

        milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.SinglesCompetitiveGain)),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2SessionsMintSinglesAndDoublesPumbilityMilestones()
    {
        // P2's title ladder gates on the per-type pools, so pool gains are milestones
        // there — alongside the total PUMBILITY gain.
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var dbl = new ChartBuilder().WithType(ChartType.Double).WithLevel(22).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var milestones = new Mock<IPlayerMilestoneRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single, dbl }, MixEnum.Phoenix2),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000), Score(dbl.Id, 960000) },
                MixEnum.Phoenix2),
            stats: stats, milestones: milestones);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix2, new[] { single.Id, dbl.Id },
            sessionId), CancellationToken.None);

        milestones.Verify(m => m.Append(MixEnum.Phoenix2, userId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.PumbilityGain && x.SessionId == sessionId)
                && w.Any(x => x.Kind == MilestoneKind.SinglesPumbilityGain)
                && w.Any(x => x.Kind == MilestoneKind.DoublesPumbilityGain)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PhoenixSessionsNeverMintPerTypePumbilityMilestones()
    {
        // Phoenix stays total-only: its S/D ratings exist too, but pre-P2 sessions never
        // minted them and shouldn't start now.
        var userId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var milestones = new Mock<IPlayerMilestoneRepository>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, milestones: milestones);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix, new[] { single.Id },
            Guid.NewGuid()), CancellationToken.None);

        milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.SinglesPumbilityGain
                           || x.Kind == MilestoneKind.DoublesPumbilityGain)),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2SkillRatingIsTheMergedTop50AcrossSinglesAndDoubles()
    {
        // Phoenix 2's official overall PUMBILITY is ONE merged top-50; each chart is
        // worth Base(level) x (grade + plate) — additive, with singles priced one level
        // up the base curve. All four charts carry the SG plate (+0.008) from the Score
        // helper.
        var userId = Guid.NewGuid();
        var s1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build(); // 995k SSS+ -> 235x1.508
        var s2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build(); // 970k S    -> 240x1.458
        var d1 = new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build(); // 950k AAA  -> 250x1.418
        var d2 = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build(); // 940k AA+  -> 245x1.398 (P2 AA+ floor)
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { s1, s2, d1, d2 }, MixEnum.Phoenix2),
            scores: ScoresMockReturning(userId, new[]
            {
                Score(s1.Id, 995000), Score(s2.Id, 970000),
                Score(d1.Id, 950000), Score(d2.Id, 940000)
            }, MixEnum.Phoenix2),
            stats: stats);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix2), CancellationToken.None);

        // Singles pool: 354.38 + 349.92 = 704.30 -> 704; Doubles pool: 354.5 + 342.51 = 697.01 -> 697.
        // Only four charts, so the merged top-50 holds all of them and Total == 704 + 697
        // here; Phoenix2SkillRatingIsAMergedTop50NotTwoPoolsSummed covers where they diverge.
        stats.Verify(s => s.SaveStats(MixEnum.Phoenix2, userId,
            It.Is<PlayerStatsRecord>(p => p.SinglesRating == 704 && p.DoublesRating == 697
                                          && p.SkillRating == 704 + 697),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2SkillRatingIsAMergedTop50NotTwoPoolsSummed()
    {
        // 30 singles (level 23) outrate 30 doubles (level 20), all clean SG plays. Each
        // per-type pool holds all 30 of its type, but the overall top-50 keeps only 50 of
        // the 60 — the 30 singles plus the 20 best doubles — so Total < Singles + Doubles.
        var userId = Guid.NewGuid();
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var vs = scoring.GetScore(ChartType.Single, DifficultyLevel.From(23), PhoenixScore.From(995000),
            PhoenixPlate.SuperbGame);
        var vd = scoring.GetScore(ChartType.Double, DifficultyLevel.From(20), PhoenixScore.From(995000),
            PhoenixPlate.SuperbGame);
        Assert.True(vs > vd, "test setup: singles must outrate doubles so doubles are the ones dropped");

        var singleCharts = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(23).Build()).ToArray();
        var doubleCharts = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build()).ToArray();
        var allCharts = singleCharts.Concat(doubleCharts).ToArray();
        var allScores = allCharts.Select(c => Score(c.Id, 995000)).ToArray();

        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var saga = BuildSaga(
            charts: ChartsMockReturning(allCharts, MixEnum.Phoenix2),
            scores: ScoresMockReturning(userId, allScores, MixEnum.Phoenix2),
            stats: stats);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix2), CancellationToken.None);

        // Accumulate the doubles exactly as production's LINQ Sum does (order + flooring).
        var expectedSingles = (int)Enumerable.Repeat(vs, 30).Sum();
        var expectedDoubles = (int)Enumerable.Repeat(vd, 30).Sum();
        var expectedTotal = (int)Enumerable.Repeat(vs, 30).Concat(Enumerable.Repeat(vd, 20)).Sum();
        stats.Verify(s => s.SaveStats(MixEnum.Phoenix2, userId,
            It.Is<PlayerStatsRecord>(p => p.SinglesRating == expectedSingles
                                          && p.DoublesRating == expectedDoubles
                                          && p.SkillRating == expectedTotal
                                          && p.SkillRating < p.SinglesRating + p.DoublesRating
                                          && p.SkillRating > p.SinglesRating),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2BrokenPlaysNeverEnterThePools()
    {
        // A broken 995k would top the singles pool if counted; Phoenix 2 excludes it, so
        // the pool is only the clean 920k AA (P2 AA floor; singles price one level up:
        // 235 x 1.378 = 323.83 -> 323).
        var userId = Guid.NewGuid();
        var broken = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var clean = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { broken, clean }, MixEnum.Phoenix2),
            scores: ScoresMockReturning(userId, new[]
            {
                Score(broken.Id, 995000, isBroken: true),
                Score(clean.Id, 920000)
            }, MixEnum.Phoenix2),
            stats: stats);

        await saga.Handle(new RecalculateStatsCommand(userId, MixEnum.Phoenix2), CancellationToken.None);

        stats.Verify(s => s.SaveStats(MixEnum.Phoenix2, userId,
            It.Is<PlayerStatsRecord>(p => p.SinglesRating == 323 && p.SkillRating == 323
                                          && p.DoublesRating == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2Top50OrdersByPlateWhenScoresTie()
    {
        // Same chart level, same score — the better plate outranks on Phoenix 2 (plates
        // are priced into the formula there, unlike Phoenix).
        var userId = Guid.NewGuid();
        var rough = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ultimate = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { rough, ultimate }, MixEnum.Phoenix2),
            scores: ScoresMockReturning(userId, new[]
            {
                Score(rough.Id, 970000, plate: PhoenixPlate.RoughGame),
                Score(ultimate.Id, 970000, plate: PhoenixPlate.UltimateGame)
            }, MixEnum.Phoenix2));

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(userId, ChartType: null, Mix: MixEnum.Phoenix2),
            CancellationToken.None)).ToArray();

        Assert.Equal(ultimate.Id, result[0].ChartId);
        Assert.Equal(rough.Id, result[1].ChartId);
    }

    [Fact]
    public async Task RecalculateMixRatingsSweepsEveryPlayerOfTheMix()
    {
        // The formula-adjustment exit path: every user with stats for the mix goes
        // through both the stats and the per-chart PUMBILITY recalculations.
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetUserIdsWithStats(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        stats.Setup(s => s.GetStats(MixEnum.Phoenix2, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(user1));
        var scores = ScoresMockReturning(user1, new[] { Score(chart.Id, 950000) }, MixEnum.Phoenix2);
        scores.Setup(s => s.GetPlayerScores(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        var recordStats = new Mock<IPhoenixRecordStatsRepository>();
        var saga = BuildSaga(charts: ChartsMockReturning(new[] { chart }, MixEnum.Phoenix2),
            scores: scores, stats: stats, recordStats: recordStats);

        await saga.Consume(BuildContext(new RecalculateMixRatingsCommand(MixEnum.Phoenix2)));

        stats.Verify(s => s.SaveStats(MixEnum.Phoenix2, user1, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        stats.Verify(s => s.SaveStats(MixEnum.Phoenix2, user2, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        recordStats.Verify(s => s.UpdateScoreStats(MixEnum.Phoenix2, user1,
            It.IsAny<IEnumerable<PhoenixRecordStats>>(), It.IsAny<CancellationToken>()), Times.Once);
        recordStats.Verify(s => s.UpdateScoreStats(MixEnum.Phoenix2, user2,
            It.IsAny<IEnumerable<PhoenixRecordStats>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PlayerRatingSaga BuildSaga(
        Mock<IScoreReader>? scores = null,
        Mock<IChartRepository>? charts = null,
        Mock<IPlayerStatsRepository>? stats = null,
        Mock<IBus>? bus = null,
        Mock<IMediator>? mediator = null,
        Mock<IPhoenixRecordStatsRepository>? recordStats = null,
        Mock<IScoreHighlightRepository>? highlights = null,
        Mock<IPlayerMilestoneRepository>? milestones = null)
    {
        scores ??= new Mock<IScoreReader>();
        charts ??= new Mock<IChartRepository>();
        stats ??= new Mock<IPlayerStatsRepository>();
        bus ??= new Mock<IBus>();
        mediator ??= new Mock<IMediator>();
        recordStats ??= new Mock<IPhoenixRecordStatsRepository>();
        highlights ??= new Mock<IScoreHighlightRepository>();
        milestones ??= new Mock<IPlayerMilestoneRepository>();
        return new PlayerRatingSaga(scores.Object, recordStats.Object, charts.Object, stats.Object,
            highlights.Object, milestones.Object, FakeDateTime.At(Now).Object, bus.Object, mediator.Object);
    }

    private static Mock<IChartRepository> ChartsMockReturning(IEnumerable<Chart> result,
        MixEnum mix = MixEnum.Phoenix)
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(mix, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    [Fact]
    public async Task RecalculateStatsIgnoresBrokenScoresEverywhere()
    {
        // A walkoff must not move any stat: competitive level (partial scores deflate small
        // accounts and deep partials on overrated charts would farm it), co-op rating (the
        // plate-blind Phoenix formula would happily rate a broken co-op), or total rating.
        // The stats computed with and without broken rows must be identical.
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var hardSingle = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build();
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var chartsMock = ChartsMockReturning(new[] { single, hardSingle, coOp });
        var userId = Guid.NewGuid();
        var passesOnly = new[] { Score(single.Id, 950000) };
        var withBroken = new[]
        {
            Score(single.Id, 950000),
            Score(hardSingle.Id, 900000, isBroken: true),
            Score(coOp.Id, 990000, isBroken: true)
        };
        PlayerStatsRecord? cleanStats = null;
        PlayerStatsRecord? dirtyStats = null;

        await BuildSaga(charts: chartsMock, scores: ScoresMockReturning(userId, passesOnly),
                stats: CapturingStats(userId, r => cleanStats = r))
            .Handle(new RecalculateStatsCommand(userId), CancellationToken.None);
        await BuildSaga(charts: chartsMock, scores: ScoresMockReturning(userId, withBroken),
                stats: CapturingStats(userId, r => dirtyStats = r))
            .Handle(new RecalculateStatsCommand(userId), CancellationToken.None);

        Assert.NotNull(cleanStats);
        Assert.Equal(cleanStats, dirtyStats);
    }

    private static Mock<IPlayerStatsRepository> CapturingStats(Guid userId, Action<PlayerStatsRecord> capture)
    {
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStats(userId));
        stats.Setup(s => s.SaveStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<PlayerStatsRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<MixEnum, Guid, PlayerStatsRecord, CancellationToken>((_, _, record, _) => capture(record))
            .Returns(Task.CompletedTask);
        return stats;
    }

    private static Mock<IScoreReader> ScoresMockReturning(Guid userId,
        IEnumerable<RecordedPhoenixScore> result, MixEnum mix = MixEnum.Phoenix)
    {
        var m = new Mock<IScoreReader>();
        m.Setup(s => s.GetBestScores(mix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        m.Setup(s => s.GetBestScores(mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score, bool isBroken = false,
        PhoenixPlate plate = PhoenixPlate.SuperbGame) =>
        new(chartId, score, plate, isBroken,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static PlayerStatsRecord ZeroStats(Guid userId) =>
        new(userId, TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
            DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0, CompetitiveLevel: 0,
            SinglesCompetitiveLevel: 0, DoublesCompetitiveLevel: 0);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
