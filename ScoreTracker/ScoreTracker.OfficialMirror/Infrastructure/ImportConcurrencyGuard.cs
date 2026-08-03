using System.Collections.Concurrent;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Infrastructure;

// Singleton: one shared set of user ids with an import in flight. TryAdd/TryRemove are atomic,
// so concurrent Start attempts race cleanly — exactly one wins the slot.
internal sealed class ImportConcurrencyGuard : IImportConcurrencyGuard
{
    // Deep scans are the only work heavy enough to need a site-wide cap. Two at a time keeps a
    // second player from waiting on a 240-page walk without ever doubling that load again.
    private const int ConcurrentDeepScans = 2;

    private readonly ConcurrentDictionary<Guid, byte> _running = new();
    private readonly SemaphoreSlim _deepScans = new(ConcurrentDeepScans, ConcurrentDeepScans);

    public bool TryBegin(Guid userId)
    {
        return _running.TryAdd(userId, 0);
    }

    public void End(Guid userId)
    {
        _running.TryRemove(userId, out _);
    }

    public bool TryBeginDeepScan()
    {
        return _deepScans.Wait(0);
    }

    public void EndDeepScan()
    {
        // Release throws once the count is back at its maximum, which is what an unbalanced
        // End would look like — swallow it rather than fail a scan that already finished.
        try
        {
            _deepScans.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }
}
