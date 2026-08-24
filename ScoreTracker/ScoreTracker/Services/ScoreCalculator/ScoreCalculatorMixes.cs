using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.ScoreCalculator;

/// <summary>
///     The mixes `/PhoenixCalculator/{mix}` exists for — those scored by the Phoenix formula,
///     newest first (docs/design/phoenix-score-calculator.md D1). One place for the list, so
///     the page's routes, the eyebrow's cross-links, the head resolver and the sitemap agree
///     on which URLs are real. The bare route is the pre-rebuild URL and keeps its inbound
///     signals; it serves the viewer's mix and canonicalises to that mix's own path.
/// </summary>
public static class ScoreCalculatorMixes
{
    public static readonly IReadOnlyList<MixEnum> All = new[] { MixEnum.Phoenix2, MixEnum.Phoenix };

    public const string Root = "/PhoenixCalculator";

    /// <summary>The self-canonical path for a mix's page: `/PhoenixCalculator/phoenix-2`.</summary>
    public static string PathFor(MixEnum mix)
    {
        return $"{Root}/{ChartSlugs.MixSlug(mix)}";
    }
}
