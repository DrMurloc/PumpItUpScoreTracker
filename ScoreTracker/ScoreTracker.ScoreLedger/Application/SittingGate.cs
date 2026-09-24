using System.Collections.Concurrent;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     One player's sitting on one mix is read and written by one caller at a time: recording plays
///     into it and closing it both hold this, so a play cannot join a sitting that is being
///     announced, and two requests arriving together cannot each open a sitting of their own.
/// </summary>
internal sealed class SittingGate
{
    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix), SemaphoreSlim> _gates = new();

    public async Task<IDisposable> Enter(Guid userId, MixEnum mix, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd((userId, mix), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Release(gate);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release();
        }
    }
}
