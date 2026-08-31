using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     WidgetHost's auto-refresh machinery (owner, 2026-08-30): each widget subscribes only to
///     the signal its data follows — import-terminal for score readers, recalculated stats for
///     analysis readers — a burst of events coalesces into one reload, and a reload that lands
///     mid-edit waits for Done. The reload itself is observable as the child re-sending its
///     load query.
/// </summary>
public sealed class WidgetHostRefreshTests : ComponentTestBase
{
    private readonly FakeUiHub _hub = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly TimeSpan _realDebounce = WidgetHost.RefreshDebounce;

    public WidgetHostRefreshTests()
    {
        // Shortened so these facts don't each wait two seconds; restored in Dispose because
        // it is assembly-wide state and xUnit runs other classes in parallel with this one.
        WidgetHost.RefreshDebounce = TimeSpan.FromMilliseconds(40);

        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        // Setups go on the base's mediator, never a replacement registration: re-registering
        // would shadow the base's own stubs (the scoring-level cache DifficultyBubble reads).
        // Enough for the hosted widgets (Account Stats 1x1, Weekly 1x1) to render quietly —
        // these facts assert on queries sent, not on widget markup.
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyTournamentChart>());
        Mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyTournamentEntry>());
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        Services.AddSingleton<IUiNotificationHub>(_hub);
        Services.AddSingleton(Mock.Of<IUserRepository>());
        // Suggested Charts dates its rows; every registration has to land here, because the
        // first render freezes the provider.
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>());
        Services.AddScoped<CommunityGlowReader>();
        Services.AddScoped<ChartCatalogCache>();
        this.RenderInteractive();
    }

    protected override void Dispose(bool disposing)
    {
        WidgetHost.RefreshDebounce = _realDebounce;
        base.Dispose(disposing);
    }

    private IRenderedComponent<WidgetHost> RenderHost(string typeId, bool editMode = false)
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), typeId, null, 0, "1x1", "{}", 1);
        return RenderComponent<WidgetHost>(p => p
            .Add(h => h.Widget, widget)
            .Add(h => h.EffectiveMix, MixEnum.Phoenix)
            .Add(h => h.EditMode, editMode));
    }

    private Task PublishStats()
    {
        return _hub.PublishAsync(UiTopics.User(_me),
            new PlayerStatsUpdatedEvent(_me, Stats(), MixEnum.Phoenix));
    }

    private Task PublishImportStatus(string status)
    {
        return _hub.PublishAsync(UiTopics.User(_me),
            new ImportStatusUpdatedEvent(_me, status, Array.Empty<RecordedPhoenixScore>(),
                MixEnum.Phoenix));
    }

    private PlayerStatsRecord Stats()
    {
        return new PlayerStatsRecord(_me, 5000, 26, 100, 0, 0,
            SkillRating: 868, SkillScore: 900000, SkillLevel: 21.5,
            SinglesRating: 852, SinglesScore: 900000, SinglesLevel: 21.3,
            DoublesRating: 774, DoublesScore: 880000, DoublesLevel: 19.9,
            CompetitiveLevel: 20.61, SinglesCompetitiveLevel: 21.34, DoublesCompetitiveLevel: 19.87);
    }

    [Fact]
    public void AnalysisWidgetsSubscribeToTheStatsSignalOnly()
    {
        RenderHost("pumbility");

        var subscription = Assert.Single(_hub.Subscriptions);
        Assert.Equal(UiTopics.User(_me), subscription.Topic);
        Assert.Equal(typeof(PlayerStatsUpdatedEvent), subscription.MessageType);
    }

    [Fact]
    public void ScoreWidgetsSubscribeToTheImportSignalOnly()
    {
        RenderHost("weekly-challenge");

        var subscription = Assert.Single(_hub.Subscriptions);
        Assert.Equal(UiTopics.User(_me), subscription.Topic);
        Assert.Equal(typeof(ImportStatusUpdatedEvent), subscription.MessageType);
    }

    [Fact]
    public async Task ABurstOfStatsEventsCoalescesIntoOneReload()
    {
        var cut = RenderHost("pumbility");
        cut.WaitForAssertion(() => Mediator.Verify(
            m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()), Times.Once));

        await PublishStats();
        await PublishStats();
        await PublishStats();

        cut.WaitForAssertion(() => Mediator.Verify(
                m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)),
            TimeSpan.FromSeconds(2));
        // Nothing further trickles in after the window closes.
        await Task.Delay(200);
        Mediator.Verify(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProgressTicksDoNotReloadOnlyTheTerminalStatusDoes()
    {
        var cut = RenderHost("weekly-challenge");
        cut.WaitForAssertion(() => Mediator.Verify(
            m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()), Times.Once));

        await PublishImportStatus("Saving 12 of 300");
        await Task.Delay(200);
        Mediator.Verify(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await PublishImportStatus("Charts finished saving");
        cut.WaitForAssertion(() => Mediator.Verify(
                m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ARefreshLandingMidEditWaitsForDone()
    {
        var cut = RenderHost("pumbility", editMode: true);
        cut.WaitForAssertion(() => Mediator.Verify(
            m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()), Times.Once));

        await PublishStats();
        await Task.Delay(200);
        // Â§2.3: nothing reloads the grid while it is being rearranged.
        Mediator.Verify(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);

        cut.SetParametersAndRender(p => p.Add(h => h.EditMode, false));

        cut.WaitForAssertion(() => Mediator.Verify(
                m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AManualRefreshStandsDownAQueuedOneInsteadOfBeingReRolledByIt()
    {
        // Shuffle inside the debounce window used to be thrown away seconds later: the queued
        // reload still fired and re-rolled the picks the player had just asked for. A wide
        // window here so the click lands mid-flight rather than racing the timer.
        WidgetHost.RefreshDebounce = TimeSpan.FromSeconds(1);
        Mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartRecommendation>());
        Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var cut = RenderHost("suggested-charts");
        cut.WaitForAssertion(() => Mediator.Verify(
            m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()), Times.Once));

        var shuffle = cut.Find(".dash-head-action button");
        await PublishStats();
        await shuffle.ClickAsync(new MouseEventArgs());

        // The shuffle reloaded once. Past the window, the auto-refresh it superseded must not
        // add a second and throw the fresh picks away.
        cut.WaitForAssertion(() => Mediator.Verify(
                m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)),
            TimeSpan.FromSeconds(2));
        await Task.Delay(1400);
        Mediator.Verify(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>
    ///     Records subscriptions and hands published messages straight to them. The real hub is
    ///     not used here because these facts assert on WHICH signal a widget subscribed to,
    ///     which only a recording double can answer — but delivery matches it, exceptions
    ///     swallowed included, so nothing here passes on semantics production lacks.
    /// </summary>
    private sealed class FakeUiHub : IUiNotificationHub
    {
        private readonly List<Entry> _subscriptions = new();

        public IReadOnlyList<Entry> Subscriptions => _subscriptions;

        public IDisposable Subscribe<T>(string topic, Func<T, Task> handler)
        {
            var entry = new Entry(topic, typeof(T), handler);
            _subscriptions.Add(entry);
            return new Unsubscriber(() => _subscriptions.Remove(entry));
        }

        public async Task PublishAsync<T>(string topic, T message)
        {
            foreach (var entry in _subscriptions.ToArray())
                if (entry.Topic == topic && entry.MessageType == typeof(T))
                    try
                    {
                        await ((Func<T, Task>)entry.Handler)(message);
                    }
                    catch
                    {
                        // Same best-effort delivery as UiNotificationHub: a torn-down circuit
                        // must not sink the publish.
                    }
        }

        internal sealed record Entry(string Topic, Type MessageType, object Handler);

        private sealed class Unsubscriber : IDisposable
        {
            private readonly Action _dispose;

            public Unsubscriber(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose();
            }
        }
    }
}
