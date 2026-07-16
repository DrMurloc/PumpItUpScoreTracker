using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The head a route serves as static HTML. OgImage/Canonical are absent for routes the
///     resolver doesn't recognise.
/// </summary>
public sealed record StaticHeadModel(string Title, string Description, string? OgImage, string? Canonical);

/// <summary>
///     Resolves the document head from the request path
///     (docs/design/seo-friendly-site.md §4). Crawlers, unfurlers and LLM readers see only
///     this head — PageTitle and HeadContent render inside a circuit they never run. In a
///     browser the circuit's PageTitle replaces the static title after boot, so titles here
///     match the page's own text and the swap never shows. Null means an unmatched route:
///     App.razor falls back to the bare site title with no description, because one shared
///     description on every URL reads as sitewide duplicate content.
/// </summary>
public sealed class StaticHeadResolver
{
    private readonly ChartUrlResolver _charts;

    public StaticHeadResolver(ChartUrlResolver charts)
    {
        _charts = charts;
    }

    public async Task<StaticHeadModel?> Resolve(PathString path, MixEnum currentMix,
        CancellationToken cancellationToken)
    {
        // /Charts/{mix}/{song}/{difficulty} — the canonical chart page. Historical triples
        // 301 to canonical before rendering, so a rendered page is always self-canonical.
        if (!path.StartsWithSegments("/Charts", out var rest)) return null;
        var segments = rest.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not { Length: 3 }) return null;

        var resolution = await _charts.ResolveHistorical(segments[0], segments[1], segments[2],
            ChartUrlResolver.DefaultMix, cancellationToken);
        if (resolution == null) return null;
        var chart = await _charts.FindChart(resolution.ChartId, currentMix, cancellationToken);
        if (chart == null) return null;

        // Title = the chart page's own PageTitle text; description = its (previously
        // circuit-only) meta description, now served where a crawler reads it.
        return new StaticHeadModel(
            $"{chart.Song.Name} {chart.DifficultyString}",
            $"Statistics and leaderboards for {chart.Song.Name} {chart.DifficultyString} by {chart.Song.Artist}.",
            chart.Song.ImagePath.ToString(),
            $"https://piuscores.arroweclip.se{resolution.CanonicalPath}");
    }
}
