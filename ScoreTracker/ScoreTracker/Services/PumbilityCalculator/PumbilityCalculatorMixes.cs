using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.PumbilityCalculator;

/// <summary>
///     The mixes `/PumbilityCalculator/{mix}` exists for — those with a PUMBILITY formula
///     (<see cref="SharedKernel.Models.ScoringConfiguration.PumbilityScoring" />), newest first.
///     One place for the list, so the page's routes, the eyebrow's cross-links, the head
///     resolver and the sitemap agree on which URLs are real.
/// </summary>
public static class PumbilityCalculatorMixes
{
    public static readonly IReadOnlyList<MixEnum> All = new[] { MixEnum.Phoenix2, MixEnum.Phoenix };

    public const string Root = "/PumbilityCalculator";

    /// <summary>The self-canonical path for a mix's page: `/PumbilityCalculator/phoenix-2`.</summary>
    public static string PathFor(MixEnum mix)
    {
        return $"{Root}/{ChartSlugs.MixSlug(mix)}";
    }
}
