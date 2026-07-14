using System.Collections.Immutable;
using System.Collections.Concurrent;

namespace ScoreTracker.Web.Services.UiNotifications;

// Singleton. Subscriptions are copy-on-write immutable sets per (message type, topic), so publishes
// iterate a stable snapshot with no lock while subscribe/unsubscribe (rare, one per page view) swap
// the set atomically.
public sealed class UiNotificationHub : IUiNotificationHub
{
    private readonly ConcurrentDictionary<(Type, string), ImmutableHashSet<object>> _subscribers = new();

    public IDisposable Subscribe<T>(string topic, Func<T, Task> handler)
    {
        var key = (typeof(T), topic);
        _subscribers.AddOrUpdate(key,
            _ => ImmutableHashSet.Create<object>(handler),
            (_, set) => set.Add(handler));
        return new Subscription(() => Remove(key, handler));
    }

    public async Task PublishAsync<T>(string topic, T message)
    {
        if (!_subscribers.TryGetValue((typeof(T), topic), out var handlers)) return;
        await Task.WhenAll(handlers.Select(async handler =>
        {
            try
            {
                await ((Func<T, Task>)handler)(message);
            }
            catch
            {
                // Best-effort UI push: a disposed circuit or a throwing handler must not sink
                // delivery to the others.
            }
        }));
    }

    private void Remove((Type, string) key, object handler)
    {
        _subscribers.AddOrUpdate(key, _ => ImmutableHashSet<object>.Empty, (_, set) => set.Remove(handler));
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;

        public Subscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
