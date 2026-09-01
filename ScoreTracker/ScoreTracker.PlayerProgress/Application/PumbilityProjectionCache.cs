using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     What one sweep produced: the peers' ladder per chart — every rung the page can ask for
    ///     (D51), read once over the same voices — the peer group per chart type it was drawn
    ///     from, and what those peers' pools are made of. Held together because all of them are
    ///     answers about the same population at the same moment: the page prints the group beside
    ///     the estimates, and lists the pools under them (docs/design/pumbility-overhaul.md §3.10).
    /// </summary>
    /// <param name="Ladders">
    ///     Per chart, the peers read at each of <see cref="EnergyRungs.All" />. The estimate a
    ///     request prints is a lookup on this at its energy, which is what makes a change of energy
    ///     free: the sweep is the expensive half and it is rung-agnostic.
    /// </param>
    /// <param name="PeerPools">
    ///     Per lit chart type, the peers' pools from the same read the estimates came from. Empty
    ///     for a dark type — absent means "no peers", never "nobody holds anything".
    /// </param>
    internal sealed record ProjectionSweep(
        IReadOnlyDictionary<Guid, PeerLadder> Ladders,
        IReadOnlyDictionary<ChartType, PeerGroup> Peers,
        IReadOnlyDictionary<ChartType, PeerPoolSummary> PeerPools);

    /// <summary>
    ///     Holds the peer sweep behind a player's Pumbility projection between visits.
    ///     <para>
    ///         What is kept is only what the sweep produced — what players around this one
    ///         score on the charts in range. Everything else the page shows is priced from it
    ///         on every visit, because the bar those estimates are measured against moves the
    ///         moment the player does. Keeping the priced result instead meant caching a
    ///         number that could be wrong by the time it was read, and re-reading the whole
    ///         Pass Count tier list per player to do it.
    ///     </para>
    ///     <para>
    ///         Pool-free by the same reasoning: which pool you are looking at changes the bar,
    ///         never the estimate, so all three selector positions share one sweep. Energy-free
    ///         too: the sweep holds every rung the chip can ask for, so the three energies share
    ///         it as well (D51).
    ///     </para>
    ///     <para>
    ///         Eviction is by the viewer's own score changes. Peers' scores moving does NOT
    ///         evict — a projection a few hours behind on other people's play is
    ///         indistinguishable from one that is not, and watching every player's imports
    ///         would evict continuously and cache nothing. <see cref="Lifetime" /> bounds that
    ///         drift.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityProjectionCache : IDisposable
    {
        /// <summary>Bounds how far behind peers' play a cached sweep can drift.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

        /// <summary>
        ///     Entries, not bytes. A backstop rather than a working limit: an entry runs a few
        ///     tens of kilobytes, so this bounds the cache well past any real day's traffic and
        ///     exists so a surge cannot grow it without limit. Evicting costs a recomputation,
        ///     which is what expiry already does.
        /// </summary>
        private const int MaxEntries = 5000;

        private readonly MemoryCache _cache;

        /// <summary>
        ///     Guards the check-then-start below. Held only across a dictionary read and the
        ///     synchronous prologue of the sweep, never across its awaits.
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        ///     Its own cache, not the shared one. <see cref="MemoryCache.Set{TItem}" /> throws
        ///     once a SizeLimit is set and an entry omits its size, so bounding the app-wide
        ///     instance would break every other caller in the solution.
        /// </summary>
        public PumbilityProjectionCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries });
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        /// <summary>
        ///     The cached sweep, computing it if nobody has. The TASK is what is cached, not
        ///     its result: the home page's suggestion widget and the Pumbility page ask for the
        ///     same thing seconds apart, and caching the result would let the second arrival
        ///     start a second sweep while the first was still running.
        /// </summary>
        public Task<ProjectionSweep> GetOrAdd(Guid userId, MixEnum mix,
            Func<Task<ProjectionSweep>> compute)
        {
            var key = Key(userId, mix);
            if (_cache.TryGetValue(key, out Task<ProjectionSweep>? running) &&
                running != null)
                return running;

            lock (_gate)
            {
                if (_cache.TryGetValue(key, out running) && running != null) return running;

                var started = Run(key, compute);
                // A sweep that fails before its first real await is already a faulted task by
                // the time control returns here, so Run's own cleanup has run and this Set
                // would put the failure back. Both orderings have to be handled; neither on
                // its own is enough.
                if (started.IsFaulted) return started;

                _cache.Set(key, started, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = Lifetime,
                    Size = 1
                });
                return started;
            }
        }

        /// <summary>
        ///     Drops a player's sweep. A null mix drops every mix — a score wipe does not say
        ///     which one it took.
        /// </summary>
        public void Evict(Guid userId, MixEnum? mix)
        {
            var mixes = mix is { } one ? new[] { one } : Enum.GetValues<MixEnum>();
            foreach (var m in mixes) _cache.Remove(Key(userId, m));
        }

        /// <summary>
        ///     A failure must not be cached. Without this the first caller's transient error
        ///     would be handed to every later one for a day, and the only cure would be a
        ///     restart.
        /// </summary>
        private async Task<ProjectionSweep> Run(string key,
            Func<Task<ProjectionSweep>> compute)
        {
            try
            {
                return await compute();
            }
            catch
            {
                _cache.Remove(key);
                throw;
            }
        }

        private static string Key(Guid userId, MixEnum mix)
        {
            return $"pumbility:estimates:{userId}:{mix}";
        }
    }
}
