using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>The head a route serves as static HTML. OgImage is absent when the route has no art.</summary>
public sealed record StaticHeadModel(string Title, string Description, string? OgImage);

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
        if (!path.StartsWithSegments("/Chart", out var rest)) return null;
        if (!Guid.TryParse(rest.Value?.Trim('/'), out var chartId)) return null;

        var chart = await _charts.FindChart(chartId, currentMix, cancellationToken);
        if (chart == null) return null;

        // The title is the chart page's own PageTitle text; the description is its
        // (previously circuit-only) meta description, now served where a crawler reads it.
        return new StaticHeadModel(
            $"{chart.Song.Name} {chart.DifficultyString}",
            $"Statistics and leaderboards for {chart.Song.Name} {chart.DifficultyString} by {chart.Song.Artist}.",
            chart.Song.ImagePath.ToString());
    }
}
