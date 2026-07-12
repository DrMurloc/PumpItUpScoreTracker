using ApexCharts;

namespace ScoreTracker.Web.Services.Theming;

/// <summary>
///     The one place ApexCharts learns the site's look (widget-shell round 5): frozen
///     canvas (no zoom/drag-select — charts here are glances, not explorers), the
///     display face, palette-driven fore color, whisper-grid, dark theme. Callers take
///     BaseOptions and layer chart-specific parts on top (stroke dashes, fills, axes,
///     annotations). Pair with <see cref="WrapperClass" /> on the chart's container —
///     the tooltip CSS keys on it, because Apex's own dark theme half-applies.
///     Existing page charts (WSIP, /Pumbility) migrate as they're touched; new graphs
///     start here.
/// </summary>
public static class ApexChartTheming
{
    /// <summary>Put this class on the element wrapping the ApexChart.</summary>
    public const string WrapperClass = "themed-apex";

    public static ApexChartOptions<TItem> BaseOptions<TItem>(MixPalette palette) where TItem : class
    {
        return new ApexChartOptions<TItem>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar { Show = false },
                Zoom = new Zoom { Enabled = false },
                Selection = new Selection { Enabled = false },
                Background = "transparent",
                FontFamily = "Barlow Condensed, Bahnschrift, Arial Narrow, sans-serif",
                ForeColor = palette.InkMuted
            },
            Grid = new Grid { BorderColor = "rgba(255,255,255,.08)" },
            Theme = new Theme { Mode = Mode.Dark }
        };
    }
}
