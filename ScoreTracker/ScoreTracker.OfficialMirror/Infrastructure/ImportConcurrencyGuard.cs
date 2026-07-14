using System.Collections.Concurrent;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Infrastructure;

// Singleton: one shared set of user ids with an import in flight. TryAdd/TryRemove are atomic,
// so concurrent Start attempts race cleanly — exactly one wins the slot.
internal sealed class ImportConcurrencyGuard : IImportConcurrencyGuard
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    public bool TryBegin(Guid userId)
    {
        return _running.TryAdd(userId, 0);
    }

    public void End(Guid userId)
    {
        _running.TryRemove(userId, out _);
    }
}
