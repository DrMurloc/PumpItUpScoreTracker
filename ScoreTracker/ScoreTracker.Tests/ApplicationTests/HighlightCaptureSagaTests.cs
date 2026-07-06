using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class HighlightCaptureSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CrownFlagsChartsInTheCurrentTop50()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenTop50(chart.Id);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlag.PumbilityTop50) && x.Level == 20)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TitleProgressFlagsScoresCountingTowardIncompleteTitles()
    {
        // A non-broken AA on a level with an incomplete difficulty title contributes
        // (PhoenixDifficultyTitle.CompletionProgress > 0) and gets the flag.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlag.TitleProgress))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreQualityFlagsTopDecileAgainstComparablePlayers()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        // Ten comparable scores, all below — tie-inclusive percentile 1.0.
        ctx.GivenCohort(chart, Enumerable.Range(0, 10).Select(i => (PhoenixScore)(900000 + i)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlag.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreQualityDoesNotFlagMidPackScores()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);
        ctx.GivenCohort(chart, Enumerable.Range(0, 10)
            .Select(i => (PhoenixScore)(905000 + i * 10000)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlag.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FolderDebutFlagsTheFirstPassesInAFolder()
    {
        // Folder has 10 charts; this is the player's second-ever pass there.
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(others[0], 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlag.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderDebutStopsAfterTheThirdPass()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        foreach (var passed in others.Take(3)) ctx.GivenBest(passed, 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlag.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FolderCompletionFlagsPassesInNearlyCompleteFolders()
    {
        // 10-chart folder, 9 passed after this batch = 90%.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        foreach (var passed in others.Take(8)) ctx.GivenBest(passed, 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlag.FolderCompletion90))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompletingAFolderFiresPassGradeAndPlateLamps()
    {
        // Two-chart D23 folder: one already passed, this pass completes it — the pass
        // lamp fires, plus the grade and plate floors that now exist.
        var chartA = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var chartB = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chartA, chartB);
        ctx.GivenBest(chartA, 981000);
        ctx.GivenBest(chartB, 970500);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chartB)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.FolderPassLamp && x.Detail == "D23")
                && w.Any(x => x.Kind == MilestoneKind.FolderGradeLamp && x.Detail == "D23|S")
                && w.Any(x => x.Kind == MilestoneKind.FolderPlateLamp && x.Detail == "D23|FairGame")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderLampsRideThePublishedEvent()
    {
        // The Discord cards' milestone banner renders from the event — the lamps the
        // capture just persisted must travel with it, not require a racing read-back.
        var chartA = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var chartB = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chartA, chartB);
        ctx.GivenBest(chartA, 981000);
        ctx.GivenBest(chartB, 970500);
        var context = ctx.Context(NewPassEvent(chartB));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Milestones.Any(m => m.Kind == MilestoneKind.FolderPassLamp && m.Detail == "D23")
                && e.Milestones.Any(m => m.Kind == MilestoneKind.FolderGradeLamp && m.Detail == "D23|S")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RatingAndTitleStepsEnrichThePublishedSnapshot()
    {
        // The orchestration (revision 2): the rating and title steps run in-process
        // before the publish, so their milestones, the ⬆ improver flag, and the
        // per-title progress deltas all ride the one snapshot event.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenRatingStep(
            new[] { new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, sessionId, Now, 100, 150, null, null) },
            chart.Id);
        ctx.GivenTitleStep(
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, sessionId, Now, null, null,
                    "Intermediate Lv. 1", null)
            },
            new TitleProgressDelta("Expert Lv. 4", 0.82, 0.86));
        var context = ctx.Context(NewPassEvent(chart, sessionId));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Changes.Single().Flags.HasFlag(HighlightFlag.CompetitiveImprover)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.PumbilityGain)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.TitleCompleted)
                && e.TitleProgress.Single().Title == "Expert Lv. 4"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedStepShipsTheSnapshotWithoutItsSection()
    {
        // Failure isolation per step: the rating step blowing up costs the stats
        // section, never the announcement — the title step's output still ships.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.Mediator.Setup(m => m.Send(It.IsAny<PlayerRatingSaga.CaptureSessionStats>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("recalc boom"));
        ctx.GivenTitleStep(new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, null, Now, null, null, "Advanced Lv. 2", null)
        });
        var context = ctx.Context(NewPassEvent(chart));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Milestones.All(m => m.Kind != MilestoneKind.PumbilityGain)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.TitleCompleted)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncompleteFoldersFireNoLamps()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<PlayerMilestoneWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishesCapturedEventCarryingFlagsAndSession()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenTop50(chart.Id);
        var context = ctx.Context(NewPassEvent(chart, sessionId));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e => e.UserId == UserId
                                                     && e.SessionId == sessionId
                                                     && e.OccurredAt == Now
                                                     && e.Changes.Single().ChartId == chart.Id
                                                     && e.Changes.Single().Flags
                                                         .HasFlag(HighlightFlag.PumbilityTop50)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishesUnFlaggedWhenCaptureItselfFails()
    {
        // Capture must never cost the announcement.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));
        var context = ctx.Context(NewPassEvent(chart));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Changes.Single().Flags == HighlightFlag.None),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<ScoreHighlightWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PlayerScoresUpdatedEvent NewPassEvent(Chart chart, Guid? sessionId = null)
    {
        return PlayerScoresUpdatedEvent.Create(Now, UserId, MixEnum.Phoenix,
            new[]
            {
                new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, IsNewPass: true, OldScore: null,
                    NewScore: 910000, Plate: "FairGame", IsBroken: false)
            }, sessionId);
    }

    private sealed class HandlerContext
    {
        private readonly List<RecordedPhoenixScore> _bests = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
        public Mock<IScoreHighlightRepository> Highlights { get; } = new();
        public Mock<IPlayerMilestoneRepository> Milestones { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public HighlightCaptureSaga Saga { get; }

        public HandlerContext()
        {
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_bests);
            Titles.Setup(t => t.GetCompletedTitles(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TitleAchievedRecord>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(UserId, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 20, 20, 20));
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
            Saga = new HighlightCaptureSaga(Charts.Object, Scores.Object, Titles.Object, PlayerStats.Object,
                Highlights.Object, Milestones.Object, Mediator.Object, new MemoryCache(new MemoryCacheOptions()),
                NullLogger<HighlightCaptureSaga>.Instance);
        }

        public void GivenCharts(params Chart[] charts)
        {
            Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        public void GivenBest(Chart chart, PhoenixScore score)
        {
            _bests.Add(new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.FairGame, false, Now));
        }

        public void GivenTop50(params Guid[] chartIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(chartIds
                    .Select(id => new RecordedPhoenixScore(id, 950000, PhoenixPlate.FairGame, false, Now))
                    .ToArray());
        }

        public void GivenRatingStep(PlayerMilestoneRecord[] milestones, params Guid[] improverChartIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<PlayerRatingSaga.CaptureSessionStats>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerRatingSaga.SessionStatsResult(milestones, improverChartIds));
        }

        public void GivenTitleStep(PlayerMilestoneRecord[] milestones, params TitleProgressDelta[] progress)
        {
            Mediator.Setup(m => m.Send(It.IsAny<TitleSaga.CaptureSessionTitles>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleSaga.SessionTitlesResult(milestones, progress));
        }

        public void GivenCohort(Chart chart, PhoenixScore[] ascendingScores)
        {
            var players = new[] { Guid.NewGuid() };
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(players);
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ascendingScores
                    .Select(s => (players[0],
                        new RecordedPhoenixScore(chart.Id, s, PhoenixPlate.FairGame, false, Now)))
                    .ToArray());
        }

        public ConsumeContext<PlayerScoresUpdatedEvent> Context(PlayerScoresUpdatedEvent message)
        {
            var ctx = new Mock<ConsumeContext<PlayerScoresUpdatedEvent>>();
            ctx.SetupGet(c => c.Message).Returns(message);
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return ctx.Object;
        }
    }
}
