using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

[ExcludeFromCodeCoverage]
public sealed class GetRecentHighlightEventsHandlerTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = When.AddDays(-7);

    private readonly Mock<IScoreHighlightRepository> _highlights = new();
    private readonly Mock<IPlayerMilestoneRepository> _milestones = new();
    private readonly Mock<IScoreReader> _scores = new();

    private GetRecentHighlightEventsHandler Handler() => new(_highlights.Object, _milestones.Object, _scores.Object);

    private void SetupHighlights(params (Guid UserId, MixEnum Mix, ScoreHighlightRecord Record)[] rows) =>
        _highlights.Setup(h => h.GetHighlightsSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    private void SetupMilestones(params (Guid UserId, MixEnum Mix, PlayerMilestoneRecord Record)[] rows) =>
        _milestones.Setup(m => m.GetFeedMilestonesSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    private void SetupBest(Guid userId, MixEnum mix, params RecordedPhoenixScore[] best) =>
        _scores.Setup(s => s.GetBestScores(mix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(best);

    private static ScoreHighlightRecord Highlight(Guid chartId, Guid session, HighlightFlags flags, int level,
        DateTimeOffset? at = null) =>
        new(chartId, session, at ?? When, flags, level, ScoringLevel: null);

    private static PlayerMilestoneRecord Milestone(Guid session, string title, DateTimeOffset at) =>
        new(MilestoneKind.TitleCompleted, session, at, OldValue: null, NewValue: null, Title: title, Detail: null);

    [Fact]
    public async Task ReconstructsOneEventPerSessionEnrichedFromCurrentBest()
    {
        var userId = Guid.NewGuid();
        var session = Guid.NewGuid();
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        SetupHighlights(
            (userId, MixEnum.Phoenix, Highlight(chartA, session, HighlightFlags.FolderDebut, 24)),
            (userId, MixEnum.Phoenix, Highlight(chartB, session, HighlightFlags.PumbilityTop50, 26)));
        SetupMilestones((userId, MixEnum.Phoenix, Milestone(session, "Expert Lv. 4", When.AddMinutes(5))));
        SetupBest(userId, MixEnum.Phoenix,
            new RecordedPhoenixScore(chartA, PhoenixScore.From(1_000_000), PhoenixPlate.PerfectGame, false, When),
            new RecordedPhoenixScore(chartB, PhoenixScore.From(995_000), null, false, When));

        var events = (await Handler().Handle(new GetRecentHighlightEventsQuery(Since), CancellationToken.None))
            .ToArray();

        var e = Assert.Single(events);
        Assert.Equal(session, e.EventId); // EventId = SessionId → idempotent re-runs
        Assert.Equal(session, e.SessionId);
        Assert.Equal(userId, e.UserId);
        Assert.Equal(MixEnum.Phoenix, e.Mix);
        Assert.Equal(When.AddMinutes(5), e.OccurredAt); // max across highlights + milestones
        Assert.Empty(e.TitleProgress);

        var a = Assert.Single(e.Changes, c => c.ChartId == chartA);
        Assert.False(a.IsNewPass);
        Assert.Equal(1_000_000, a.NewScore);
        Assert.Equal(PhoenixPlate.PerfectGame.GetName(), a.Plate);
        Assert.False(a.IsBroken);

        Assert.Equal("Expert Lv. 4", Assert.Single(e.Milestones).Title);
    }

    [Fact]
    public async Task AMilestoneOnlySessionStillYieldsAnEventWithNoChanges()
    {
        var userId = Guid.NewGuid();
        var session = Guid.NewGuid();
        SetupHighlights();
        SetupMilestones((userId, MixEnum.Phoenix, Milestone(session, "SCROOGE", When)));

        var events = (await Handler().Handle(new GetRecentHighlightEventsQuery(Since), CancellationToken.None))
            .ToArray();

        var e = Assert.Single(events);
        Assert.Equal(session, e.EventId);
        Assert.Empty(e.Changes);
        Assert.Single(e.Milestones);
        _scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never); // no highlights → no best-score enrichment reads
    }

    [Fact]
    public async Task AHighlightWithoutACurrentBestHasNullScoreAndPlateAndBrokenPropagates()
    {
        var userId = Guid.NewGuid();
        var session = Guid.NewGuid();
        var unscored = Guid.NewGuid();
        var broken = Guid.NewGuid();
        SetupHighlights(
            (userId, MixEnum.Phoenix, Highlight(unscored, session, HighlightFlags.FolderDebut, 20)),
            (userId, MixEnum.Phoenix, Highlight(broken, session, HighlightFlags.ScoreQuality90, 21)));
        SetupMilestones();
        // Only the broken chart has a current best; the other is absent from the map.
        SetupBest(userId, MixEnum.Phoenix,
            new RecordedPhoenixScore(broken, PhoenixScore.From(950_000), null, true, When));

        var events = (await Handler().Handle(new GetRecentHighlightEventsQuery(Since), CancellationToken.None))
            .ToArray();

        var e = Assert.Single(events);
        var noBest = Assert.Single(e.Changes, c => c.ChartId == unscored);
        Assert.Null(noBest.NewScore);
        Assert.Null(noBest.Plate);
        Assert.False(noBest.IsBroken);

        var brokenChange = Assert.Single(e.Changes, c => c.ChartId == broken);
        Assert.Equal(950_000, brokenChange.NewScore);
        Assert.Null(brokenChange.Plate);
        Assert.True(brokenChange.IsBroken);
    }
}
