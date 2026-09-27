using System.Collections.Concurrent;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

// Singleton: one shared map of the users with an import in flight, and when each user last started one
// on each mix. TryAdd/TryRemove are atomic, so concurrent Start attempts race cleanly — exactly one wins.
internal sealed class ImportConcurrencyGuard : IImportConcurrencyGuard
{
    /// <summary>
    ///     How long after a run starts before the next may start on the same mix (owner, 2026-09-27: to stop
    ///     button spam overloading piugame).
    /// </summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    // Deep scans are the only work heavy enough to need a site-wide cap. Two at a time keeps a
    // second player from waiting on a 240-page walk without ever doubling that load again.
    private const int ConcurrentDeepScans = 2;

    private readonly ConcurrentDictionary<Guid, MixEnum> _running = new();
    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix), DateTimeOffset> _lastStarted = new();
    private readonly SemaphoreSlim _deepScans = new(ConcurrentDeepScans, ConcurrentDeepScans);

    public ImportSlot TryBegin(Guid userId, MixEnum mix, DateTimeOffset now, bool cooldownApplies)
    {
        if (_running.ContainsKey(userId)) return ImportSlot.AlreadyRunning;
        // The clock before the slot, so a refusal never holds the slot — not even for the moment a
        // concurrent save on the mix would read as an import running.
        if (cooldownApplies && _lastStarted.TryGetValue((userId, mix), out var started) &&
            now - started < Cooldown)
            return new ImportSlot(ImportSlotOutcome.CoolingDown, Cooldown - (now - started));
        return _running.TryAdd(userId, mix) ? ImportSlot.Taken : ImportSlot.AlreadyRunning;
    }

    public void Started(Guid userId, MixEnum mix, DateTimeOffset at)
    {
        _lastStarted[(userId, mix)] = at;
    }

    public void End(Guid userId)
    {
        _running.TryRemove(userId, out _);
    }

    public bool IsRunning(Guid userId, MixEnum mix)
    {
        return _running.TryGetValue(userId, out var running) && running == mix;
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
