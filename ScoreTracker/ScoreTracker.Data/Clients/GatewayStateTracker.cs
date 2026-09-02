using System.Diagnostics;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Turns a socket client's Connected / Disconnected transitions into a
///     <see cref="BotGatewayStatus" />. Downtime is a duration on a monotonic clock, not a
///     wall-clock stamp, so a clock adjustment can neither fake nor hide an outage; the
///     timestamp source is injectable so the arithmetic tests without waiting.
///     Discord.Net raises Disconnected on every cycle of its reconnect loop, so a repeat
///     Disconnected keeps the original stamp — the clock counts from the drop, not from the
///     latest failed attempt.
/// </summary>
public sealed class GatewayStateTracker
{
    private readonly object _gate = new();
    private readonly Func<long> _timestamp;
    private long _disconnectedAt;
    private BotGatewayState _state = BotGatewayState.NotStarted;

    public GatewayStateTracker() : this(Stopwatch.GetTimestamp)
    {
    }

    public GatewayStateTracker(Func<long> timestamp)
    {
        _timestamp = timestamp;
    }

    public BotGatewayStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _state switch
                {
                    BotGatewayState.NotStarted => BotGatewayStatus.NotStarted,
                    BotGatewayState.Connected => BotGatewayStatus.Connected,
                    _ => BotGatewayStatus.DisconnectedSince(
                        Stopwatch.GetElapsedTime(_disconnectedAt, _timestamp()))
                };
            }
        }
    }

    /// <summary>The socket is starting: not connected yet, and the downtime clock starts now.</summary>
    public void Starting()
    {
        lock (_gate)
        {
            _state = BotGatewayState.Disconnected;
            _disconnectedAt = _timestamp();
        }
    }

    public void Connected()
    {
        lock (_gate)
        {
            _state = BotGatewayState.Connected;
        }
    }

    public void Disconnected()
    {
        lock (_gate)
        {
            if (_state == BotGatewayState.Disconnected) return;
            _state = BotGatewayState.Disconnected;
            _disconnectedAt = _timestamp();
        }
    }
}
