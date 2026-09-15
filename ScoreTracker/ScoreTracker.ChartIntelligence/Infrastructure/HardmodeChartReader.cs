using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Caching;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

/// <summary>
///     The Domain port over the same storage the vertical's own handlers read
///     (docs/design/hardmode-leaderboard.md D13) — so a vertical on the wrong side of the
///     reference chain gets the list without a project reference, and gets exactly the list the
///     census wrote rather than its own idea of one.
/// </summary>
internal sealed class HardmodeChartReader : IHardmodeChartReader
{
    /// <summary>
    ///     Long, because the list is a WEEKLY fact and the census evicts this key the moment it
    ///     writes a new one. The window only has to cover the gap left by a rebuild that failed
    ///     to publish its event.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    private readonly IMemoryCache _cache;
    private readonly IHardmodeChartRepository _repository;

    public HardmodeChartReader(IHardmodeChartRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    /// <summary>
    ///     A Mix key rather than a Viewer one by the builder's own contract: the week's list is a
    ///     catalog fact and must never vary by who is looking.
    /// </summary>
    internal static string CacheKey(MixEnum mix) => CacheKeys.Mix(nameof(HardmodeChartReader), mix);

    /// <summary>
    ///     Cached because D24 changed who reads this. It used to be one read per Hardmode page
    ///     load; a glow on every difficulty bubble makes it a read on nearly every page on the
    ///     site, at ~1,200 rows on Phoenix 2 (D26).
    ///     <para>
    ///         The entry carries an explicit expiration: the two-argument Set silently drops the
    ///         TTL, which would be fatal here because the only other eviction is a weekly event.
    ///     </para>
    /// </summary>
    public async Task<IReadOnlyList<HardmodeChartEntry>> GetQualifyingCharts(MixEnum mix,
        CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(CacheKey(mix), async entry =>
        {
            // Relative, so the cache measures expiry on its own clock.
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var charts = await _repository.Get(mix, cancellationToken);
            return (IReadOnlyList<HardmodeChartEntry>)charts
                .Select(c => new HardmodeChartEntry(c.ChartId, c.ChartType, c.Level, c.Points, c.Holders,
                    c.FolderSize, c.FolderCut)).ToArray();
        }))!;
    }
}
