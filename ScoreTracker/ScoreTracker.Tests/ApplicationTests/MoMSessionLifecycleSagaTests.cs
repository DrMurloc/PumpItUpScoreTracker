using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Events;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MoMSessionLifecycleSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Board = Guid.NewGuid();

    [Fact]
    public async Task SaveReplaysEntriesUnderTheBoardRulesAndKeepsPlayedAt()
    {
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var playedAt = Now.AddHours(-3);
        var context = new Context().WithCharts(chart);

        await context.Saga.Handle(new SaveMoMSessionDraftCommand(Board, null,
            new[] { new MoMDraftEntry(chart.Id, 990000, PhoenixPlate.SuperbGame, false, playedAt) },
            new Uri("https://youtu.be/x")), CancellationToken.None);

        context.Mom.Verify(m => m.UpsertSession(
            It.Is<MoMSessionRecord>(s =>
                s.Id == Guid.Empty && s.BoardId == Board && s.UserId == context.UserId &&
                s.PublishedAt == null && s.TotalScore == 1000 && s.ChartsPlayed == 1 &&
                Math.Abs(s.AverageDifficulty - 20.5) < 0.001 &&
                s.VideoUrl == "https://youtu.be/x"),
            It.Is<IReadOnlyList<MoMSessionChartRecord>>(rows =>
                rows.Count == 1 && rows[0].ChartId == chart.Id && rows[0].Score == 990000 &&
                rows[0].Plate == "SuperbGame" && rows[0].PlayedAt == playedAt &&
                rows[0].SessionScore == 1000),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveWithoutAnIdLandsOnTheExistingDraft()
    {
        var context = new Context();
        var draftId = Guid.NewGuid();
        context.Mom.Setup(m => m.GetDraft(Board, context.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(draftId, Board, context.UserId, null, 0, 0, 0, 0,
                0, 0, 0, null));

        await context.Saga.Handle(
            new SaveMoMSessionDraftCommand(Board, null, Array.Empty<MoMDraftEntry>(), null),
            CancellationToken.None);

        // One open draft per board (§10): the id-less save updates it, never opens a second.
        context.Mom.Verify(m => m.UpsertSession(It.Is<MoMSessionRecord>(s => s.Id == draftId),
            It.IsAny<IReadOnlyList<MoMSessionChartRecord>>(), Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveRefusesAPublishedSessionAnEndedSeasonAndAStranger()
    {
        var context = new Context();
        var published = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(published, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(published, Board, context.UserId,
                Now.AddDays(-1), 500, 1, 0, 20.5, 12, 20, 20, null));
        await Assert.ThrowsAsync<MoMSessionRuleException>(() => context.Saga.Handle(
            new SaveMoMSessionDraftCommand(Board, published, Array.Empty<MoMDraftEntry>(), null),
            CancellationToken.None));

        var stranger = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(stranger, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(stranger, Board, Guid.NewGuid(), null, 0, 0, 0, 0,
                0, 0, 0, null));
        await Assert.ThrowsAsync<NotAuthorizedException>(() => context.Saga.Handle(
            new SaveMoMSessionDraftCommand(Board, stranger, Array.Empty<MoMDraftEntry>(), null),
            CancellationToken.None));

        context.EndSeason();
        await Assert.ThrowsAsync<MoMSessionRuleException>(() => context.Saga.Handle(
            new SaveMoMSessionDraftCommand(Board, null, Array.Empty<MoMDraftEntry>(), null),
            CancellationToken.None));
    }

    [Fact]
    public async Task PublishStampsTheClockAndFiresTheEventOnce()
    {
        var context = new Context();
        var draft = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(draft, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(draft, Board, context.UserId, null, 500, 1, 0,
                20.5, 12, 20, 20, null));

        await context.Saga.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        context.Mom.Verify(m => m.PublishSession(draft, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        context.Bus.Verify(b => b.Publish(
            It.Is<MoMSessionPublishedEvent>(e =>
                e.SessionId == draft && e.BoardId == Board && e.UserId == context.UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishRefusesEmptyDraftsAndAlreadyPublishedSessions()
    {
        var context = new Context();
        var empty = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(empty, Board, context.UserId, null, 0, 0, 0, 0, 0,
                0, 0, null));
        await Assert.ThrowsAsync<MoMSessionRuleException>(() =>
            context.Saga.Handle(new PublishMoMSessionCommand(empty), CancellationToken.None));

        var published = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(published, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(published, Board, context.UserId, Now.AddDays(-1),
                500, 1, 0, 20.5, 12, 20, 20, null));
        await Assert.ThrowsAsync<MoMSessionRuleException>(() =>
            context.Saga.Handle(new PublishMoMSessionCommand(published), CancellationToken.None));

        context.Bus.Verify(b => b.Publish(It.IsAny<MoMSessionPublishedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuardsOwnershipAndIsQuietOnAMissingSession()
    {
        var context = new Context();
        var mine = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(mine, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(mine, Board, context.UserId, Now.AddDays(-1), 500,
                1, 0, 20.5, 12, 20, 20, null));
        await context.Saga.Handle(new DeleteMoMSessionCommand(mine), CancellationToken.None);
        context.Mom.Verify(m => m.DeleteSession(mine, It.IsAny<CancellationToken>()), Times.Once);

        var theirs = Guid.NewGuid();
        context.Mom.Setup(m => m.GetSession(theirs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMSessionRecord(theirs, Board, Guid.NewGuid(), Now.AddDays(-1),
                500, 1, 0, 20.5, 12, 20, 20, null));
        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            context.Saga.Handle(new DeleteMoMSessionCommand(theirs), CancellationToken.None));

        // A vanished session deletes to the same place deleting it again would: quietly.
        await context.Saga.Handle(new DeleteMoMSessionCommand(Guid.NewGuid()),
            CancellationToken.None);
        context.Mom.Verify(m => m.DeleteSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class Context
    {
        private readonly Mock<IChartRepository> _charts = new();
        private readonly Mock<ICurrentUserAccessor> _currentUser = new();
        private readonly Mock<IMediator> _mediator = new();
        private DateTimeOffset _seasonEnd = Now.AddDays(30);

        public Context()
        {
            UserId = Guid.NewGuid();
            var user = new UserBuilder().WithId(UserId).WithName("Player").Build();
            _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
            _currentUser.Setup(c => c.User).Returns(user);

            Mom.Setup(m => m.GetBoardConfiguration(Board, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Configuration());
            Mom.Setup(m => m.GetBoardConfiguration(Board, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Configuration());
            Mom.Setup(m => m.GetSeasonSnapshot(Board, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            Mom.Setup(m => m.UpsertSession(It.IsAny<MoMSessionRecord>(),
                    It.IsAny<IReadOnlyList<MoMSessionChartRecord>>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid());
            _mediator.Setup(m => m.Send(It.IsAny<GetMoMSessionQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MoMSessionView(Guid.NewGuid(), Board,
                    new MoMSeasonRef(Guid.NewGuid(), "Summer 2026", 2026, 3), MixEnum.Phoenix,
                    ChartType.Double, UserId, "Player", null, 0, 0, TimeSpan.Zero, 0, 0, 0, 0,
                    null, null, TimeSpan.FromMinutes(105), false,
                    Array.Empty<MoMSessionChartRow>()));
        }

        public Guid UserId { get; }
        public Mock<IMoMRepository> Mom { get; } = new();
        public Mock<IBus> Bus { get; } = new();

        public MoMSessionLifecycleSaga Saga => new(Mom.Object, _charts.Object,
            _currentUser.Object, FakeDateTime.At(Now).Object, Bus.Object, _mediator.Object);

        public Context WithCharts(params Chart[] charts)
        {
            _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null,
                    It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            return this;
        }

        public void EndSeason()
        {
            _seasonEnd = Now.AddDays(-1);
        }

        private TournamentConfiguration Configuration()
        {
            // Neutral modifiers: a chart prices at exactly its level rating, so the derived
            // cache assertions are clean arithmetic (level 20 = 1000).
            var scoring = new ScoringConfiguration
            {
                AdjustToTime = false,
                ContinuousLetterGradeScale = false,
                StageBreakModifier = 1.0
            };
            foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
                scoring.LetterGradeModifiers[grade] = 1.0;
            foreach (var plate in Enum.GetValues<PhoenixPlate>())
                scoring.PlateModifiers[plate] = 1.0;
            scoring.LevelRatings[DifficultyLevel.From(20)] = 1000;
            scoring.LevelRatings[DifficultyLevel.From(21)] = 1100;
            return new TournamentConfiguration(Board, "Summer 2026", scoring, false, true)
            {
                MaxTime = TimeSpan.FromMinutes(105),
                AllowRepeats = false,
                StartDate = Now.AddDays(-30),
                EndDate = _seasonEnd
            };
        }
    }
}
