using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Events;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MatchSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TournamentId = Guid.NewGuid();

    [Fact]
    public async Task HandleCreateMatchLinkPersistsTheLink()
    {
        var matches = new Mock<IMatchRepository>();
        var saga = BuildSaga(matches: matches);
        var link = new MatchLink(Guid.NewGuid(), Name.From("A"), Name.From("B"),
            IsWinners: true, PlayerCount: 2, Skip: 0);

        await saga.Handle(new CreateMatchLinkCommand(TournamentId, link), CancellationToken.None);

        matches.Verify(m => m.SaveMatchLink(TournamentId, link, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDeleteMatchLinkRemovesTheLink()
    {
        var matches = new Mock<IMatchRepository>();
        var saga = BuildSaga(matches: matches);
        var linkId = Guid.NewGuid();

        await saga.Handle(new DeleteMatchLinkCommand(linkId), CancellationToken.None);

        matches.Verify(m => m.DeleteMatchLink(linkId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSaveRandomSettingsPersistsByTournamentAndName()
    {
        var matches = new Mock<IMatchRepository>();
        var saga = BuildSaga(matches: matches);
        var settings = new RandomSettings();

        await saga.Handle(
            new SaveRandomSettingsCommand(TournamentId, Name.From("Mix"), settings),
            CancellationToken.None);

        matches.Verify(m => m.SaveRandomSettings(TournamentId,
            It.Is<Name>(n => (string)n == "Mix"), settings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMatchPersistsAndPublishesMatchUpdatedEvent()
    {
        var matches = new Mock<IMatchRepository>();
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(matches: matches, mediator: mediator);
        var view = NewMatch("Final");

        await saga.Handle(new UpdateMatchCommand(TournamentId, view), CancellationToken.None);

        matches.Verify(m => m.SaveMatch(TournamentId, view, It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(
            It.Is<MatchUpdatedEvent>(e => e.TournamentId == TournamentId && e.NewState == view),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleGetMatchReturnsRepositoryValue()
    {
        var view = NewMatch("Semi");
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.Is<Name>(n => (string)n == "Semi"),
            It.IsAny<CancellationToken>())).ReturnsAsync(view);
        var saga = BuildSaga(matches: matches);

        var result = await saga.Handle(new GetMatchQuery(TournamentId, Name.From("Semi")),
            CancellationToken.None);

        Assert.Same(view, result);
    }

    [Fact]
    public async Task HandleResolveMatchTransitionsStateToFinalizingAndPublishes()
    {
        var view = NewMatch("Quarter") with { State = MatchState.InProgress };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(matches: matches, mediator: mediator);

        await saga.Handle(new ResolveMatchCommand(TournamentId, Name.From("Quarter")),
            CancellationToken.None);

        matches.Verify(m => m.SaveMatch(TournamentId,
            It.Is<MatchView>(v => v.State == MatchState.Finalizing && v.LastUpdated == Now),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(
            It.Is<MatchUpdatedEvent>(e => e.NewState.State == MatchState.Finalizing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleFinishCardDrawSetsStateReadyAndZeroesScoresAndPoints()
    {
        var alice = Name.From("alice");
        var bob = Name.From("bob");
        var chart1 = Guid.NewGuid();
        var chart2 = Guid.NewGuid();
        var view = NewMatch("Round 1") with
        {
            State = MatchState.CardDraw,
            Players = new[] { alice, bob },
            ActiveCharts = new[] { chart1, chart2 }
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(matches: matches, mediator: mediator);

        await saga.Handle(new FinishCardDrawCommand(TournamentId, Name.From("Round 1")),
            CancellationToken.None);

        matches.Verify(m => m.SaveMatch(TournamentId, It.Is<MatchView>(v =>
                v.State == MatchState.Ready
                && v.Scores.Count == 2
                && v.Scores["alice"].Length == 2 && v.Scores["alice"].All(s => s == (PhoenixScore)0)
                && v.Points["bob"].Length == 2 && v.Points["bob"].All(p => p == 0)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMatchScoresUpdatesNestedScoreAndPublishes()
    {
        var alice = Name.From("alice");
        var bob = Name.From("bob");
        var chartId = Guid.NewGuid();
        var view = NewMatch("Round 1") with
        {
            State = MatchState.Ready,
            Players = new[] { alice, bob },
            ActiveCharts = new[] { chartId },
            Scores = new Dictionary<string, PhoenixScore[]>
            {
                ["alice"] = new[] { (PhoenixScore)0 },
                ["bob"] = new[] { (PhoenixScore)0 }
            },
            Points = new Dictionary<string, int[]>
            {
                ["alice"] = new[] { 0 },
                ["bob"] = new[] { 0 }
            }
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(matches: matches, mediator: mediator);

        await saga.Handle(
            new UpdateMatchScoresCommand(TournamentId, Name.From("Round 1"), alice, ChartIndex: 0,
                NewScore: 950000),
            CancellationToken.None);

        matches.Verify(m => m.SaveMatch(TournamentId, It.Is<MatchView>(v =>
                v.Scores["alice"][0] == (PhoenixScore)950000 && v.Scores["bob"][0] == (PhoenixScore)0),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<MatchUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMatchScoresSendsDiscordWhenSongFirstCompletes()
    {
        var alice = Name.From("alice");
        var bob = Name.From("bob");
        var chartId = Guid.NewGuid();
        // Alice already has a score; Bob's submission is what completes the song.
        var view = NewMatch("Round 1") with
        {
            State = MatchState.Ready,
            Players = new[] { alice, bob },
            ActiveCharts = new[] { chartId },
            Scores = new Dictionary<string, PhoenixScore[]>
            {
                ["alice"] = new[] { (PhoenixScore)900000 },
                ["bob"] = new[] { (PhoenixScore)0 }
            },
            Points = new Dictionary<string, int[]>
            {
                ["alice"] = new[] { 0 },
                ["bob"] = new[] { 0 }
            }
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartBuilder().WithId(chartId).Build());
        var qualifiers = QualifiersMockReturning(notificationChannel: 12345);
        var bot = new Mock<IBotClient>();
        var saga = BuildSaga(matches: matches, charts: charts, qualifiers: qualifiers, bot: bot);

        await saga.Handle(
            new UpdateMatchScoresCommand(TournamentId, Name.From("Round 1"), bob, ChartIndex: 0,
                NewScore: 950000),
            CancellationToken.None);

        bot.Verify(b => b.SendMessage(It.IsAny<string>(), 12345ul, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMatchScoresDoesNotSendDiscordWhenSongAlreadyComplete()
    {
        var alice = Name.From("alice");
        var bob = Name.From("bob");
        var chartId = Guid.NewGuid();
        var view = NewMatch("Round 1") with
        {
            State = MatchState.Ready,
            Players = new[] { alice, bob },
            ActiveCharts = new[] { chartId },
            Scores = new Dictionary<string, PhoenixScore[]>
            {
                ["alice"] = new[] { (PhoenixScore)900000 },
                ["bob"] = new[] { (PhoenixScore)850000 }
            },
            Points = new Dictionary<string, int[]>
            {
                ["alice"] = new[] { 1 },
                ["bob"] = new[] { 0 }
            }
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        var bot = new Mock<IBotClient>();
        var saga = BuildSaga(matches: matches, bot: bot);

        await saga.Handle(
            new UpdateMatchScoresCommand(TournamentId, Name.From("Round 1"), bob, ChartIndex: 0,
                NewScore: 990000),
            CancellationToken.None);

        bot.Verify(b => b.SendMessage(It.IsAny<string>(), It.IsAny<ulong>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(MatchState.NotStarted, "Card draw is ready")]
    [InlineData(MatchState.Ready, "is ready to play")]
    [InlineData(MatchState.InProgress, "TOs are requesting")]
    public async Task HandlePingMatchTailorsMessageToCurrentState(MatchState state, string expectedSubstring)
    {
        var alice = Name.From("alice");
        var view = NewMatch("Round 1") with
        {
            State = state,
            Players = new[] { alice }
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        matches.Setup(m => m.GetMatchPlayers(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new MatchPlayer(alice, Seed: 1, DiscordId: 999ul) });
        var qualifiers = QualifiersMockReturning(notificationChannel: 12345);
        var bot = new Mock<IBotClient>();
        var saga = BuildSaga(matches: matches, qualifiers: qualifiers, bot: bot);

        await saga.Handle(new PingMatchCommand(TournamentId, Name.From("Round 1")),
            CancellationToken.None);

        bot.Verify(b => b.SendMessage(It.Is<string>(s => s.Contains(expectedSubstring)),
            12345ul, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDrawChartsForCoOpEasyOverridesChartIdsWithEasyConstantBeforeRandomDraw()
    {
        // The "CoOp Easy" branch swaps ChartIds for the EasyCoOp constant before delegating
        // chart selection to GetRandomChartsQuery. We pin that GetRandomChartsQuery is sent
        // with the overridden, non-empty ChartIds set rather than whatever was on the
        // original RandomSettings record.
        var view = NewMatch("Round 1") with
        {
            State = MatchState.NotStarted,
            RandomSettings = Name.From("CoOp Easy")
        };
        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        matches.Setup(m => m.GetRandomSettings(TournamentId, It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RandomSettings()); // No ChartIds preset.
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var saga = BuildSaga(matches: matches, mediator: mediator);

        await saga.Handle(new DrawChartsCommand(TournamentId, Name.From("Round 1")),
            CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetRandomChartsQuery>(q => q.Settings.ChartIds.Count > 0),
            It.IsAny<CancellationToken>()), Times.Once);
        matches.Verify(m => m.SaveMatch(TournamentId,
            It.Is<MatchView>(v => v.State == MatchState.CardDraw),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleFinalizeMatchPromotesWinnersAndCompletesState()
    {
        var alice = Name.From("alice");
        var bob = Name.From("bob");
        var unknown1 = Name.From("Unknown 1");
        var unknown2 = Name.From("Unknown 2");
        var sourceMatch = NewMatch("Round 1") with
        {
            State = MatchState.Finalizing,
            FinalPlaces = new[] { alice, bob },
            Players = new[] { alice, bob },
            Points = new Dictionary<string, int[]>
            {
                ["alice"] = new[] { 1 },
                ["bob"] = new[] { 0 }
            }
        };
        var nextMatch = NewMatch("Round 2") with
        {
            Players = new[] { unknown1, unknown2 }
        };
        var link = new MatchLink(Guid.NewGuid(), Name.From("Round 1"), Name.From("Round 2"),
            IsWinners: true, PlayerCount: 1, Skip: 0);

        var matches = new Mock<IMatchRepository>();
        matches.Setup(m => m.GetMatch(TournamentId, It.Is<Name>(n => (string)n == "Round 1"),
            It.IsAny<CancellationToken>())).ReturnsAsync(sourceMatch);
        matches.Setup(m => m.GetMatch(TournamentId, It.Is<Name>(n => (string)n == "Round 2"),
            It.IsAny<CancellationToken>())).ReturnsAsync(nextMatch);
        matches.Setup(m => m.GetMatchLinksByFromMatchName(TournamentId, It.IsAny<Name>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { link });
        var saga = BuildSaga(matches: matches);

        await saga.Handle(new FinalizeMatchCommand(TournamentId, Name.From("Round 1")),
            CancellationToken.None);

        // Round 2 gets alice promoted into the first Unknown slot.
        matches.Verify(m => m.SaveMatch(TournamentId,
            It.Is<MatchView>(v => (string)v.MatchName == "Round 2" && v.Players[0] == alice),
            It.IsAny<CancellationToken>()), Times.Once);
        // Round 1 itself transitions to Completed.
        matches.Verify(m => m.SaveMatch(TournamentId,
            It.Is<MatchView>(v => (string)v.MatchName == "Round 1" && v.State == MatchState.Completed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MatchSaga BuildSaga(
        Mock<IMediator>? mediator = null,
        Mock<IMatchRepository>? matches = null,
        Mock<IAdminNotificationClient>? admins = null,
        Mock<IChartRepository>? charts = null,
        Mock<IBotClient>? bot = null,
        Mock<IQualifiersRepository>? qualifiers = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        mediator ??= new Mock<IMediator>();
        matches ??= new Mock<IMatchRepository>();
        admins ??= new Mock<IAdminNotificationClient>();
        charts ??= new Mock<IChartRepository>();
        bot ??= new Mock<IBotClient>();
        qualifiers ??= new Mock<IQualifiersRepository>();
        dateTime ??= FakeDateTime.At(Now);
        return new MatchSaga(mediator.Object, matches.Object, admins.Object, charts.Object,
            bot.Object, qualifiers.Object, dateTime.Object);
    }

    private static Mock<IQualifiersRepository> QualifiersMockReturning(ulong notificationChannel)
    {
        var m = new Mock<IQualifiersRepository>();
        m.Setup(q => q.GetQualifiersConfiguration(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QualifiersConfiguration(
                charts: Array.Empty<Chart>(),
                adjustments: new Dictionary<Guid, int>(),
                scoringType: Name.From("default"),
                notificationChannel: notificationChannel,
                playCount: 1,
                cutoffTime: null,
                allCharts: false));
        return m;
    }

    private static MatchView NewMatch(string name) =>
        new(MatchName: Name.From(name), PhaseName: Name.From("Phase"), MatchOrder: 1, ChartCount: 0,
            RandomSettings: Name.From("Default"), State: MatchState.NotStarted,
            Players: Array.Empty<Name>(), ActiveCharts: Array.Empty<Guid>(),
            VetoedCharts: Array.Empty<Guid>(), ProtectedCharts: Array.Empty<Guid>(),
            Scores: new Dictionary<string, PhoenixScore[]>(),
            Points: new Dictionary<string, int[]>(),
            FinalPlaces: Array.Empty<Name>());
}
