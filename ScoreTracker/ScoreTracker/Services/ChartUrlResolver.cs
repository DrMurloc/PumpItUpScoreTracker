using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services;

/// <summary>Where a requested chart URL actually lives.</summary>
public sealed record ChartUrlResolution(Guid ChartId, string CanonicalPath);

/// <summary>
///     The redirect lattice's brain (docs/design/chart-details-overhaul.md), built dark at
///     B4 — the P3 controller wires it to routes. Two jobs: the canonical path for a chart
///     (the site's default mix when the chart exists there, else the newest mix carrying
///     it), and historical resolution — a (mix, song, level) triple is resolved against
///     THAT mix's catalog, because the mix segment timestamps the level: /xx/…/d19 means
///     "the chart that was D19 in XX", unambiguous across cross-mix renumbering. The
///     current holder of a level owns its URL (within-mix rebalance tiebreak).
/// </summary>
public sealed class ChartUrlResolver
{
    private static readonly IReadOnlyDictionary<string, MixEnum> MixesBySlug =
        Enum.GetValues<MixEnum>().ToDictionary(ChartSlugs.MixSlug, m => m);

    private static readonly MixEnum[] NewestFirst =
        Enum.GetValues<MixEnum>().OrderByDescending(m => m.DisplayOrder()).ToArray();

    private readonly IMemoryCache _cache;
    private readonly IMediator _mediator;

    public ChartUrlResolver(IMediator mediator, IMemoryCache cache)
    {
        _mediator = mediator;
        _cache = cache;
    }

    /// <summary>
    ///     The chart a GUID names: the preferred mix's copy when it exists there, else the
    ///     newest mix carrying it — the same order the canonical path resolves in.
    /// </summary>
    public async Task<Chart?> FindChart(Guid chartId, MixEnum preferredMix,
        CancellationToken cancellationToken)
    {
        var chart = await FindInMix(preferredMix, chartId, cancellationToken);
        if (chart == null)
            foreach (var mix in NewestFirst)
            {
                if (mix == preferredMix) continue;
                chart = await FindInMix(mix, chartId, cancellationToken);
                if (chart != null) break;
            }

        return chart;
    }

    /// <summary>
    ///     The canonical path the GUID permalink 301s to. Null only for unknown charts.
    /// </summary>
    public async Task<string?> CanonicalPathFor(Guid chartId, MixEnum defaultMix,
        CancellationToken cancellationToken)
    {
        var chart = await FindChart(chartId, defaultMix, cancellationToken);
        return chart == null ? null : ChartSlugs.CanonicalPath(chart);
    }

    /// <summary>
    ///     Resolves any /{mix}/{song}/{difficulty} triple — canonical, historical, or
    ///     stale-slugged — to the chart it names and that chart's current canonical.
    /// </summary>
    public async Task<ChartUrlResolution?> ResolveHistorical(string mixSlug, string songSlug,
        string difficultySlug, MixEnum defaultMix, CancellationToken cancellationToken)
    {
        if (!MixesBySlug.TryGetValue(mixSlug.ToLowerInvariant(), out var mix)) return null;
        var song = songSlug.ToLowerInvariant();
        var difficulty = difficultySlug.ToLowerInvariant();
        var match = (await Charts(mix, cancellationToken)).FirstOrDefault(c =>
            ChartSlugs.DifficultySlug(c) == difficulty && ChartSlugs.SlugifySong(c.Song.Name) == song);
        if (match == null) return null;

        var canonical = await CanonicalPathFor(match.Id, defaultMix, cancellationToken);
        return canonical == null ? null : new ChartUrlResolution(match.Id, canonical);
    }

    private async Task<Chart?> FindInMix(MixEnum mix, Guid chartId, CancellationToken cancellationToken)
    {
        return (await Charts(mix, cancellationToken)).FirstOrDefault(c => c.Id == chartId);
    }

    private async Task<IReadOnlyList<Chart>> Charts(MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync($"ChartUrlResolver__{mix}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return (IReadOnlyList<Chart>)(await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToArray();
        }))!;
    }
}
