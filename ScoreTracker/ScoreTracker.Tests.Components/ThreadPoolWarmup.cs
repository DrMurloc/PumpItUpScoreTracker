using System.Runtime.CompilerServices;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Raises the thread pool's worker floor for this assembly, which makes bUnit's
///     synchronous event helpers reliable enough to assert against.
///     <para>
///         bUnit posts every event dispatch (<c>Click</c>, <c>Change</c>, <c>Input</c>) to
///         the renderer's dispatcher, which schedules it on the thread pool; the
///         synchronous overloads return without waiting for it. xUnit runs one collection
///         per processor and parks each on a synchronous test body, so on a many-core box
///         every pool worker is blocked at the pool's minimum and a queued dispatch waits
///         on the thread-injection throttle rather than running. The test thread reaches
///         its assertion first and reads the pre-event render. Giving the pool room to
///         grow on demand means a dispatch always finds a worker.
///     </para>
///     <para>
///         This widens the window; it does not remove the race — a test that must be
///         certain the handler ran awaits <c>ClickAsync</c>/<c>ChangeAsync</c> instead
///         (see <see cref="RandomizerSettingsPanelTests" />). Past roughly four concurrent
///         copies of the suite the machine is CPU-oversubscribed rather than
///         thread-starved, and no floor helps.
///     </para>
/// </summary>
internal static class ThreadPoolWarmup
{
    [ModuleInitializer]
    internal static void RaiseTheWorkerFloor()
    {
        // One worker per parked collection, plus headroom for the dispatches they queue.
        // Threads are created on demand, so this is a ceiling on un-throttled growth
        // rather than an allocation.
        ThreadPool.GetMinThreads(out _, out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount * 4, 64), completionPorts);
    }
}
