using System;
using System.Threading.Tasks;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class UiNotificationHubTests
{
    private sealed record Msg(int Value);

    private sealed record OtherMsg(int Value);

    [Fact]
    public async Task DeliversToSubscribersOfTheSameTopicAndType()
    {
        var hub = new UiNotificationHub();
        var received = 0;
        hub.Subscribe<Msg>("t", m =>
        {
            received = m.Value;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("t", new Msg(7));

        Assert.Equal(7, received);
    }

    [Fact]
    public async Task DoesNotDeliverToADifferentTopic()
    {
        var hub = new UiNotificationHub();
        var received = false;
        hub.Subscribe<Msg>("a", _ =>
        {
            received = true;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("b", new Msg(1));

        Assert.False(received);
    }

    [Fact]
    public async Task DoesNotDeliverToADifferentMessageTypeOnTheSameTopic()
    {
        var hub = new UiNotificationHub();
        var received = false;
        hub.Subscribe<Msg>("t", _ =>
        {
            received = true;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("t", new OtherMsg(1));

        Assert.False(received);
    }

    [Fact]
    public async Task DisposeStopsFurtherDelivery()
    {
        var hub = new UiNotificationHub();
        var count = 0;
        var subscription = hub.Subscribe<Msg>("t", _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("t", new Msg(1));
        subscription.Dispose();
        await hub.PublishAsync("t", new Msg(1));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeliversToEverySubscriberOnATopic()
    {
        var hub = new UiNotificationHub();
        int a = 0, b = 0;
        hub.Subscribe<Msg>("t", _ =>
        {
            a++;
            return Task.CompletedTask;
        });
        hub.Subscribe<Msg>("t", _ =>
        {
            b++;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("t", new Msg(1));

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public async Task AThrowingSubscriberDoesNotStopTheOthers()
    {
        var hub = new UiNotificationHub();
        var reached = false;
        hub.Subscribe<Msg>("t", _ => throw new InvalidOperationException("boom"));
        hub.Subscribe<Msg>("t", _ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await hub.PublishAsync("t", new Msg(1));

        Assert.True(reached);
    }

    [Fact]
    public async Task PublishingToATopicWithNoSubscribersIsHarmless()
    {
        var hub = new UiNotificationHub();

        await hub.PublishAsync("nobody-home", new Msg(1));
    }
}
