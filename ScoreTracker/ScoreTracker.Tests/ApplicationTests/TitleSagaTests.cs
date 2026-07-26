using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TitleSagaTests
{
    [Fact]
    public async Task TitleProgressForPhoenix2BuildsFromThePhoenix2List()
    {
        // The real Phoenix 2 catalog (crawled from the live title.php) — its own list,
        // never a Phoenix fallthrough.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var progress =
            (await ctx.Saga.Handle(new GetTitleProgressQuery(MixEnum.Phoenix2), CancellationToken.None)).ToArray();

        Assert.Contains(progress, p => p.Title.Name == "[S] INTERMEDIATE LV.1");
        Assert.DoesNotContain(progress, p => p.Title.Name == "Intermediate Lv. 1"); // Phoenix-only
    }

    [Fact]
    public async Task TitleProgressForPhoenixStillBuildsFromThePhoenixList()
    {
        // Contrast case: the same setup under Phoenix produces the real (non-empty) list.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var progress = await ctx.Saga.Handle(new GetTitleProgressQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.NotEmpty(progress);
    }

    [Fact]
    public async Task TitleProgressForAnUnknownMixThrowsInsteadOfFallingBackToPhoenix()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ctx.Saga.Handle(new GetTitleProgressQuery((MixEnum)999), CancellationToken.None));
    }

    [Fact]
    public async Task Phoenix2ScoreCaptureBelowEveryThresholdCompletesNothingButReportsPoolProgress()
    {
        // One 999k on an unremarkable L20 single: no P2 title completes (the pool value is
        // far below the 5000+ ladder floor and the chart matches no skill/boss title), no
        // legacy event fires, and highest-difficulty stays untouched (P2's ladder isn't
        // level-keyed). The PUMBILITY ladder still MOVED, so progress deltas report it.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 999000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix2, null,
                new[] { new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 999000, "SuperbGame", false) }),
            CancellationToken.None);

        ctx.Titles.Verify(t => t.SaveTitles(MixEnum.Phoenix2, ctx.UserId,
            It.Is<IEnumerable<TitleAchievedRecord>>(titles => !titles.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Titles.Verify(t => t.SetHighestDifficultyTitle(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.Name>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains(result.Progress, d => d.Title == "[S] INTERMEDIATE LV.1");
    }

    [Fact]
    public async Task CaptureSuppressesTheLegacyAnnouncementTheCardNowCarries()
    {
        // Score-driven completions ride the snapshot card; the legacy Discord message must
        // NOT also fire (it survives only on the detected-titles path). Another Truth S6 is
        // the [The 1st] boss breaker — passing it crosses the title incomplete → done, which
        // the batch-crossing detects even though the site path may have saved it first.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(6)
            .WithSongName("Another Truth").Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 950000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.Contains(result.Milestones, m => m.Kind == MilestoneKind.TitleCompleted);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProgressDeltasReportTitlesThatMovedTowardCompletion()
    {
        // Real per-title deltas (owner call): the before-state reconstructs from the
        // change's old score, and only titles whose rounded percent moved make the list.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 999000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, false, 500000, 999000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.NotEmpty(result.Progress);
        Assert.All(result.Progress, d => Assert.True(d.NewPercent > d.OldPercent));
        Assert.True(result.Progress.Count <= 5);
    }

    [Fact]
    public async Task ContiguousLadderFloorsKeepAllButTheActiveRungOffTheProgressList()
    {
        // The reported bug: one single score reported progress on EVERY [S] pumbility rung at
        // once ("[S] INTERMEDIATE LV.1 0% → 19%, LV.2 0% → 16%, LV.3 0% → 13%") because the delta
        // percent divided by the raw requirement, ignoring the floor. Ladder floors are
        // contiguous (a rung floors on the rung below's requirement), so at most one rung can be
        // mid-progress — no ladder may report two rungs moving at once.
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(23).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, single);
        ctx.GivenBestScores(Score(single.Id, 985000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix2, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(single.Id, false, 500000, 985000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.Contains(result.Progress, d => d.Title.StartsWith("[S]")); // the [S] ladder is exercised
        var doubledUp = result.Progress
            .GroupBy(LadderBase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} -> {string.Join(", ", g.Select(d => d.Title))}")
            .ToArray();
        Assert.True(doubledUp.Length == 0,
            "A ladder reported multiple rungs moving at once (floor ignored): " + string.Join(" | ", doubledUp));
    }

    [Fact]
    public async Task BatchCompletionsAreOrderedByLadderThenRung()
    {
        // A big batch crosses many rungs at once. They must arrive grouped by ladder with the
        // lowest rung first, so the card reads like the climb it was — the milestone table has no
        // ORDER BY, so this ordering is the only thing standing between the player and a jumble
        // like "BRONZE, D LV.1, D LV.2, SILVER, S LV.5, D LV.3".
        var charts = Enumerable.Range(0, 50)
            .SelectMany(_ => new[]
            {
                new ChartBuilder().WithType(ChartType.Single).WithLevel(26).Build(),
                new ChartBuilder().WithType(ChartType.Double).WithLevel(26).Build()
            })
            .ToArray();
        var ctx = new SagaContext(MixEnum.Phoenix2, charts);
        ctx.GivenBestScores(charts.Select(c => Score(c.Id, 999000)).ToArray());

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix2, null,
                charts.Select(c => new PlayerScoresUpdatedEvent.ScoreChange(c.Id, true, null, 999000,
                    "SuperbGame", false)).ToArray()),
            CancellationToken.None);

        var pumbilityRungs = result.Milestones
            .Where(m => m.Kind == MilestoneKind.TitleCompleted)
            .Select(m => Phoenix2TitleList.GetTitleByName(Name.From(m.Title!)))
            .OfType<Phoenix2PumbilityTitle>()
            .Select(t => (Ladder: t.Pool, t.CompletionRequired))
            .ToArray();

        // Enough rungs to make ordering meaningful, across more than one ladder.
        Assert.True(pumbilityRungs.Length > 3, $"expected several rungs, got {pumbilityRungs.Length}");
        Assert.True(pumbilityRungs.Select(r => r.Ladder).Distinct().Count() > 1, "expected multiple ladders");

        var expected = pumbilityRungs
            .OrderBy(r => r.Ladder == PumbilityPool.Total ? 0 : r.Ladder == PumbilityPool.Singles ? 1 : 2)
            .ThenBy(r => r.CompletionRequired)
            .ToArray();
        Assert.Equal(expected, pumbilityRungs);
    }

    // Strip a trailing "LV.N" / "Lv. N" rung number so sibling rungs collapse to one ladder key.
    private static string LadderBase(TitleProgressDelta delta) =>
        System.Text.RegularExpressions.Regex.Replace(delta.Title, @"\s*[Ll][Vv]\.?\s*\d+$", "").Trim();

    private sealed class SagaContext
    {
        private readonly MixEnum _mix;
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IPlayerMilestoneRepository> Milestones { get; } = new();
        public Mock<IPlayerScoreBatchAccumulator> Batches { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public TitleSaga Saga { get; }

        public SagaContext(MixEnum mix, params Chart[] charts)
        {
            _mix = mix;
            CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
            CurrentUser.SetupGet(c => c.User).Returns(new UserBuilder().WithId(UserId).Build());
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<ScoreTracker.SharedKernel.ValueTypes.DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            Titles.Setup(t => t.GetCompletedTitles(mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TitleAchievedRecord>());
            Scores.Setup(s => s.GetBestScores(mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            // Default: nothing parked, and no batch open to park into — the fallback path.
            Batches.Setup(b => b.TakeDetectedTitles(It.IsAny<MixEnum>(), It.IsAny<Guid>()))
                .Returns(Array.Empty<string>());

            Saga = new TitleSaga(CurrentUser.Object, Scores.Object, Charts.Object, Titles.Object,
                Milestones.Object, FakeDateTime.At(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero)).Object,
                Batches.Object, Bus.Object);
        }

        /// <summary>An open score batch accepts the parked badges (the import saved scores).</summary>
        public void GivenAnOpenBatch()
        {
            Batches.Setup(b => b.TryAddDetectedTitles(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                It.IsAny<IEnumerable<string>>())).Returns(true);
        }

        public void GivenParkedBadges(params string[] titles)
        {
            Batches.Setup(b => b.TakeDetectedTitles(It.IsAny<MixEnum>(), It.IsAny<Guid>())).Returns(titles);
        }

        public void GivenBestScores(params RecordedPhoenixScore[] scores)
        {
            Scores.Setup(s => s.GetBestScores(_mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores);
        }

        public void GivenSessionMilestones(Guid sessionId, params PlayerMilestoneRecord[] milestones)
        {
            Milestones.Setup(m => m.GetMilestonesBySessions(It.IsAny<Guid>(),
                    It.Is<IEnumerable<Guid>>(ids => ids.Contains(sessionId)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(milestones);
        }
    }

    [Fact]
    public async Task DetectedBasicBadgesAreCapturedAsMilestonesAndAnnounced()
    {
        // The fallback route for site-only badges (CompletionRequired == 0: events, play/plate
        // counts): an import that saved no scores has no open batch to park them on and no
        // snapshot card coming, so they must take a card of their own rather than be swallowed.
        var ctx = new SagaContext(MixEnum.Phoenix);

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "RISE CHALLENGER" }, MixEnum.Phoenix)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x =>
                x.Kind == MilestoneKind.TitleCompleted && x.Title == "RISE CHALLENGER")),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DetectedComputableTitlesAreSavedButLeftToTheScorePath()
    {
        // A difficulty title the site reports (CompletionRequired > 0) is score-computable,
        // so the site path saves it (DB stays correct) but does NOT announce or milestone it —
        // the session card carries it via the score path instead.
        var ctx = new SagaContext(MixEnum.Phoenix);

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "Intermediate Lv. 1" }, MixEnum.Phoenix)));

        ctx.Titles.Verify(t => t.SaveTitles(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<TitleAchievedRecord>>(titles =>
                titles.Any(x => x.Title.ToString() == "Intermediate Lv. 1")),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<PlayerMilestoneWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectedBadgesParkOnTheOpenBatchInsteadOfTakingTheirOwnCard()
    {
        // With a score batch open, a site-only badge is parked on it so the ONE snapshot card
        // carries it alongside the scores — no card of its own. Still written as a milestone
        // against the session so the Sessions page groups it.
        var sessionId = Guid.NewGuid();
        var ctx = new SagaContext(MixEnum.Phoenix);
        ctx.GivenAnOpenBatch();

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "RISE CHALLENGER" }, MixEnum.Phoenix, sessionId)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x =>
                x.Kind == MilestoneKind.TitleCompleted && x.Title == "RISE CHALLENGER"
                && x.SessionId == sessionId)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Batches.Verify(b => b.TryAddDetectedTitles(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<string>>(t => t.Contains("RISE CHALLENGER"))), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParkedBadgesRideTheCardAfterTheLaddersAndAreTakenOnlyOnce()
    {
        // The title step collects whatever the site path parked. Badges carry no requirement to
        // climb, so they trail the ladder crossings, alphabetically. Taking them is what stops the
        // next batch in the same session from announcing them again.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(6).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));
        ctx.GivenParkedBadges("THE BLACK", "LOVERS (Silver)");

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix,
                Guid.NewGuid(),
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 950000, "SuperbGame", false)
                }),
            CancellationToken.None);

        var titles = result.Milestones.Where(m => m.Kind == MilestoneKind.TitleCompleted)
            .Select(m => m.Title).ToArray();
        Assert.Equal(new[] { "LOVERS (Silver)", "THE BLACK" }, titles.TakeLast(2));
        ctx.Batches.Verify(b => b.TakeDetectedTitles(MixEnum.Phoenix, ctx.UserId), Times.Once);
        // Parked badges are already persisted by the site path — the card records must not
        // re-append them.
        ctx.Milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x => x.Title == "THE BLACK")),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureReturnsOnlyThisBatchsCompletionsNotTheWholeSessions()
    {
        // A session envelope lasts 8 hours and a score batch drains after 2 minutes, so one
        // session emits many cards. Reading the session's milestone rows back out made every
        // card repeat the previous ones' titles — the batch's own crossings are the answer, and
        // rows already written against the session must NOT come back.
        var when = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(6)
            .WithSongName("Another Truth").Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));
        ctx.GivenSessionMilestones(sessionId,
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, sessionId, when, null, null,
                "RISE CHALLENGER", null),
            new PlayerMilestoneRecord(MilestoneKind.ParagonLevelGain, sessionId, when, null, null,
                "Expert Lv. 2", "PG"));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix,
                sessionId,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 950000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.DoesNotContain(result.Milestones, m => m.Title == "RISE CHALLENGER");
        Assert.DoesNotContain(result.Milestones, m => m.Title == "Expert Lv. 2");
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score) =>
        new(chartId, score, PhoenixPlate.SuperbGame, false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
