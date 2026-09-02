using System;
using System.Diagnostics;
using ScoreTracker.Data.Clients;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GatewayStateTrackerTests
{
    private long _now = 1_000_000;

    private static long Ticks(TimeSpan span)
    {
        return (long)(span.TotalSeconds * Stopwatch.Frequency);
    }

    private GatewayStateTracker Tracker()
    {
        return new GatewayStateTracker(() => _now);
    }

    [Fact]
    public void ReportsNotStartedUntilToldOtherwise()
    {
        Assert.Equal(BotGatewayStatus.NotStarted, Tracker().Status);
    }

    [Fact]
    public void StartingCountsAsDisconnectedFromThatMoment()
    {
        var tracker = Tracker();
        tracker.Starting();
        _now += Ticks(TimeSpan.FromSeconds(90));

        var status = tracker.Status;

        Assert.Equal(BotGatewayState.Disconnected, status.State);
        Assert.InRange(status.DisconnectedFor, TimeSpan.FromSeconds(89), TimeSpan.FromSeconds(91));
    }

    [Fact]
    public void ConnectedReportsNoDowntime()
    {
        var tracker = Tracker();
        tracker.Starting();
        _now += Ticks(TimeSpan.FromMinutes(1));
        tracker.Connected();

        Assert.Equal(BotGatewayStatus.Connected, tracker.Status);
    }

    [Fact]
    public void DowntimeCountsFromTheDropNotFromLaterDisconnectEvents()
    {
        var tracker = Tracker();
        tracker.Starting();
        tracker.Connected();
        tracker.Disconnected();
        _now += Ticks(TimeSpan.FromMinutes(3));
        // Discord.Net's reconnect loop raises Disconnected on every failed cycle.
        tracker.Disconnected();
        _now += Ticks(TimeSpan.FromMinutes(2));

        var status = tracker.Status;

        Assert.Equal(BotGatewayState.Disconnected, status.State);
        Assert.InRange(status.DisconnectedFor,
            TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReconnectingResetsTheClock()
    {
        var tracker = Tracker();
        tracker.Starting();
        tracker.Connected();
        tracker.Disconnected();
        _now += Ticks(TimeSpan.FromMinutes(4));
        tracker.Connected();
        tracker.Disconnected();
        _now += Ticks(TimeSpan.FromSeconds(30));

        Assert.InRange(tracker.Status.DisconnectedFor, TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(31));
    }
}
