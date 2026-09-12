using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     What one band's read produced: the cohort's spread over the levels, how their fifties
    ///     fall across the archetypes and their average merged fifty by chart type where the pool is
    ///     merged, and when the board half was swept. No viewer anywhere in it — that is the point
    ///     (docs/design/pumbility-overhaul.md D68, D69).
    /// </summary>
    internal sealed record CohortReading(CohortLevelSpread Spread, CohortArchetypeSpread Archetypes,
        PoolTypeSplit? Split, DateTimeOffset? BoardAsOf);

    /// <summary>
    ///     Holds what the players standing on one band of a PUMBILITY ladder are made of, between
    ///     visits (docs/design/pumbility-overhaul.md D68).
    ///     <para>
    ///         Keyed on the mix, the pool and the band rather than on the viewer, because a cohort is
    ///         the same answer for everyone reading it: every DIAMOND player is looking at the same
    ///         five hundred players. That is the whole saving — one read serves a band's worth of
    ///         readers instead of one read each — and it is why what is held is the aggregate rather
    ///         than the pools it was folded from. The viewer's own fifty is placed onto it per
    ///         request, which is cheap and moves the moment they play.
    ///     </para>
    ///     <para>
    ///         Nothing evicts it. A cohort drifts as its members play and as the board is re-swept,
    ///         and a census a few hours behind other people's play is indistinguishable from one
    ///         that is not — the same reasoning the projection sweep is held on, and
    ///         <see cref="Lifetime" /> bounds it the same way. Watching every import would evict
    ///         continuously and cache nothing, since a band holds hundreds of players.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityCohortCache : IDisposable
    {
        /// <summary>Bounds how far behind a band's own play and the board's sweep a reading can drift.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

        /// <summary>
        ///     Entries, not bytes. There are only so many bands — thirty-seven merged, thirty-one a
        ///     typed ladder, a few dozen Phoenix 1 titles — so this sits well past every band of
        ///     every pool on both mixes and exists only so nothing can grow without limit.
        /// </summary>
        private const int MaxEntries = 500;

        private readonly MemoryCache _cache;

        /// <summary>
        ///     Guards the check-then-start below. Held only across a dictionary read and the
        ///     synchronous prologue of the read, never across its awaits.
        /// </summary>
        private readonly object _gate = new();

        /// <summary>Its own cache, not the shared one: a SizeLimit belongs to the instance that sets it.</summary>
        public PumbilityCohortCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries });
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        /// <summary>
        ///     The cached reading, computing it if nobody has. The TASK is cached, not its result:
        ///     a band's readers arrive together — that is what a band is — and caching the result
        ///     would let every arrival during the first read start a read of its own.
        /// </summary>
        public Task<CohortReading> GetOrAdd(MixEnum mix, PumbilityPool pool, Name band,
            Func<Task<CohortReading>> compute)
        {
            var key = $"pumbility:cohort:{mix}:{pool}:{band}";
            if (_cache.TryGetValue(key, out Task<CohortReading>? running) && running != null) return running;

            lock (_gate)
            {
                if (_cache.TryGetValue(key, out running) && running != null) return running;

                var started = Run(key, compute);
                // A read that fails before its first real await is already a faulted task by the
                // time control returns here, so Run's own cleanup has run and this Set would put
                // the failure back. Both orderings have to be handled; neither on its own is enough.
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
        ///     A failure must not be cached. Without this the first reader's transient error would be
        ///     handed to every later one for a day, and the only cure would be a restart.
        /// </summary>
        private async Task<CohortReading> Run(string key, Func<Task<CohortReading>> compute)
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
    }
}
