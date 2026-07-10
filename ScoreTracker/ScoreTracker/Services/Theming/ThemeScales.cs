using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.Theming;

/// <summary>
/// Rarity bands: how good is this relative to the population. Higher percentile = rarer.
/// Band names deliberately name the color (see <see cref="RarityRamp"/>).
/// </summary>
public enum RarityBand
{
    Common,
    Silver,
    Emerald,
    Gold,
    Sapphire,
    Prism
}

/// <summary>
/// The single façade for the site's two semantic color scales — rarity (common→elite)
/// and difficulty (easy→hard). All methods return <c>var(--…)</c> token references, never
/// literal colors: the active theme's emitted :root block supplies the hues, so consumers
/// stay theme-blind. Replaces RankingColors, RatingColorSelector's RGB interpolation, and
/// the per-page TierListColor switch copies.
/// </summary>
public static class ThemeScales
{
    /// <summary>
    /// Percentile is "fraction of the comparable population at or below you" — the
    /// established ScoreRankingRecord.Ranking semantic (1.0 = first place).
    /// </summary>
    public static RarityBand BandFor(double percentile) => percentile switch
    {
        < .25 => RarityBand.Common,
        < .50 => RarityBand.Silver,
        < .75 => RarityBand.Emerald,
        < .90 => RarityBand.Gold,
        < .99 => RarityBand.Sapphire,
        _ => RarityBand.Prism
    };

    public static string CssVar(RarityBand band) => band switch
    {
        RarityBand.Common => "var(--rarity-common)",
        RarityBand.Silver => "var(--rarity-silver)",
        RarityBand.Emerald => "var(--rarity-emerald)",
        RarityBand.Gold => "var(--rarity-gold)",
        RarityBand.Sapphire => "var(--rarity-sapphire)",
        _ => "var(--rarity-prism)"
    };

    /// <summary>Inline-style fragment ("color: …;"), matching the old RankingColors shape.</summary>
    public static string RarityStyle(double? percentile) =>
        percentile == null ? string.Empty : $"color:{CssVar(BandFor(percentile.Value))};";

    /// <summary>
    /// Glow class implementing the monotonic treatment ramp — brightness of hue alone
    /// can't order the bands, so glow does (accessibility: color is never the only channel).
    /// </summary>
    public static string RarityClass(RarityBand band) => band switch
    {
        RarityBand.Gold => "rarity-glow-1",
        RarityBand.Sapphire => "rarity-glow-2",
        RarityBand.Prism => "rarity-glow-3",
        _ => string.Empty
    };

    public static string DifficultyColor(TierListCategory category) =>
        $"var(--diff-{DifficultySlug(category)})";

    internal static string DifficultySlug(TierListCategory category) => category switch
    {
        TierListCategory.Overrated => "overrated",
        TierListCategory.VeryEasy => "very-easy",
        TierListCategory.Easy => "easy",
        TierListCategory.Medium => "medium",
        TierListCategory.Hard => "hard",
        TierListCategory.VeryHard => "very-hard",
        TierListCategory.Underrated => "underrated",
        _ => "unrecorded"
    };

    /// <summary>
    /// Percentile coloring against a concrete population (community leaderboards).
    /// Zeroes are excluded from the curve — unrated players shouldn't drag it — and
    /// color as Common.
    /// </summary>
    public static PopulationScale ScaleFrom(IEnumerable<int> population) => new(population);

    public sealed class PopulationScale
    {
        private readonly int[] _sorted;

        internal PopulationScale(IEnumerable<int> population)
        {
            _sorted = population.Where(v => v != 0).OrderBy(v => v).ToArray();
        }

        public string GetColor(int value)
        {
            if (value <= 0 || _sorted.Length == 0) return CssVar(RarityBand.Common);
            // Fraction of the population at or below this value = the Ranking semantic.
            var upper = _sorted.Length;
            var lower = 0;
            while (lower < upper)
            {
                var mid = (lower + upper) / 2;
                if (_sorted[mid] <= value) lower = mid + 1;
                else upper = mid;
            }

            return CssVar(BandFor(lower / (double)_sorted.Length));
        }
    }
}
