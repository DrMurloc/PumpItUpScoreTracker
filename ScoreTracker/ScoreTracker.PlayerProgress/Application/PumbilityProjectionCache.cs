using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     Holds a player's Pumbility projection between visits.
    ///     <para>
    ///         The projection is the expensive read on the page by an order of magnitude — a
    ///         cohort sweep plus a level-history read, both sized by the player population
    ///         rather than by the viewer. Its inputs change only when somebody's scores do,
    ///         so recomputing it per visit and per pool switch bought nothing.
    ///     </para>
    ///     <para>
    ///         Eviction is by the viewer's own score changes (owner, 2026-08-06: an import
    ///         busts it). Peers' scores moving does NOT evict — a projection that is a few
    ///         hours behind on other people's play is indistinguishable from one that is not,
    ///         and watching every player's imports would evict continuously and cache nothing.
    ///         <see cref="Lifetime" /> is the backstop that bounds that drift.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityProjectionCache
    {
        /// <summary>Bounds how far behind peers' play a cached projection can drift.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

        private readonly IMemoryCache _cache;

        public PumbilityProjectionCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGet(Guid userId, MixEnum mix, ChartType? pool, out PumbilityProjection? projection)
        {
            return _cache.TryGetValue(Key(userId, mix, pool), out projection);
        }

        public void Set(Guid userId, MixEnum mix, ChartType? pool, PumbilityProjection projection)
        {
            _cache.Set(Key(userId, mix, pool), projection, Lifetime);
        }

        /// <summary>
        ///     Drops every pool a player could be looking at. A null mix drops every mix — a
        ///     score wipe does not say which one it took.
        /// </summary>
        public void Evict(Guid userId, MixEnum? mix)
        {
            var mixes = mix is { } one ? new[] { one } : Enum.GetValues<MixEnum>();
            foreach (var m in mixes)
            foreach (var pool in new ChartType?[] { null, ChartType.Single, ChartType.Double })
                _cache.Remove(Key(userId, m, pool));
        }

        // Enumerated rather than prefix-scanned: IMemoryCache cannot evict by prefix, and the
        // key space per player is small and fixed (mixes x the three pools).
        private static string Key(Guid userId, MixEnum mix, ChartType? pool)
        {
            return $"pumbility:projection:{userId}:{mix}:{pool?.ToString() ?? "all"}";
        }
    }
}
