using ScoreTracker.Domain.Services;
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

    /// <summary>
    ///     The PUMBILITY lens on the rarity ramp: it measures worth playing, not difficulty, and
    ///     rarity is the ramp whose meaning is "better" (docs/design/pumbility-tier-list.md §7).
    ///     Six stops against seven tiers, so the bottom rung shares the unrecorded grey with the
    ///     charts nobody pools at all — growing a shared token for one lens was rejected.
    /// </summary>
    public static string PumbilityColor(TierListCategory category) => category switch
    {
        TierListCategory.Overrated => "var(--rarity-prism)",
        TierListCategory.VeryEasy => "var(--rarity-sapphire)",
        TierListCategory.Easy => "var(--rarity-gold)",
        TierListCategory.Medium => "var(--rarity-emerald)",
        TierListCategory.Hard => "var(--rarity-silver)",
        TierListCategory.VeryHard => "var(--rarity-common)",
        _ => "var(--diff-unrecorded)"
    };

    /// <summary>Plate color token; null = unplayed ("plate-none").</summary>
    public static string PlateColor(PhoenixPlate? plate) =>
        plate == null
            ? "var(--plate-none)"
            : $"var(--plate-{plate.Value.GetShorthand().ToLowerInvariant()})";

    /// <summary>
    /// Grade color token. Grades ride the plate metal ladder by tier (UX-GUIDELINES §1,
    /// sampled from the Play Data art): SSS+/SSS ice-blue, SS/S gold, AAA+/AAA silver,
    /// AA/A copper, and everything below A the in-game sub-A green. This is the token
    /// sibling of <see cref="MixThemes.GradeHex"/>, for markup that can read CSS vars.
    /// </summary>
    public static string GradeColor(PhoenixLetterGrade grade) => grade switch
    {
        PhoenixLetterGrade.SSSPlus => "var(--plate-pg)",
        PhoenixLetterGrade.SSS => "var(--plate-ug)",
        PhoenixLetterGrade.SSPlus => "var(--plate-eg)",
        PhoenixLetterGrade.SS => "var(--plate-eg)",
        PhoenixLetterGrade.SPlus => "var(--plate-sg)",
        PhoenixLetterGrade.S => "var(--plate-sg)",
        PhoenixLetterGrade.AAAPlus => "var(--plate-mg)",
        PhoenixLetterGrade.AAA => "var(--plate-tg)",
        PhoenixLetterGrade.AAPlus => "var(--plate-fg)",
        PhoenixLetterGrade.AA => "var(--plate-fg)",
        PhoenixLetterGrade.APlus => "var(--plate-rg)",
        PhoenixLetterGrade.A => "var(--plate-rg)",
        _ => "var(--grade-sub-a)"
    };

    /// <summary>
    /// Legacy slot color token — the pre-Exceed song-wheel vocabulary (Crazy red,
    /// Freestyle green…). Another-variants read as their base slot; null = the neutral
    /// legacy chip (Half-Double, levelled co-ops). Never the difficulty ramp: old-scale
    /// levels don't translate to modern ones (docs/design/legacy-mixes.md).
    /// </summary>
    public static string SlotColor(LegacySlot? slot) => $"var(--slot-{SlotSlug(slot)})";

    internal static string SlotSlug(LegacySlot? slot) => slot switch
    {
        LegacySlot.Easy => "easy",
        LegacySlot.Normal or LegacySlot.AnotherNormal => "normal",
        LegacySlot.Hard or LegacySlot.AnotherHard => "hard",
        LegacySlot.Crazy or LegacySlot.AnotherCrazy => "crazy",
        LegacySlot.Freestyle or LegacySlot.AnotherFreestyle => "freestyle",
        LegacySlot.Nightmare or LegacySlot.AnotherNightmare => "nightmare",
        LegacySlot.Practice => "practice",
        LegacySlot.Another => "another",
        _ => "neutral"
    };

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
    /// Judgment color token — the game's own vocabulary (perfect ice-blue, great green,
    /// good amber, bad violet, miss red). Mix-invariant like the alert colors: a MISS has
    /// to read as a miss in every theme.
    /// </summary>
    public static string JudgmentColor(Judgment judgment) =>
        $"var(--judg-{judgment.ToString().ToLowerInvariant()})";

    /// <summary>
    /// The lifebar's zone tokens. The rainbow paints the visible bar (0–1000); overflow is
    /// the cool chrome above it, which the cabinet never shows you
    /// (docs/design/life-calculator-redesign.md).
    /// </summary>
    public static string LifeRainbow => "var(--life-rainbow)";

    public static string LifeOverflow => "var(--life-overflow)";

    public static string LifeDanger => "var(--life-danger)";

    /// <summary>
    ///     How fast a chart is FOR ITS FOLDER — five bands, slowest (0) to fastest (4)
    ///     (docs/design/chart-identity.md §2). Its own ramp, deliberately not the difficulty
    ///     one: a slow chart at a high level is not an easy one, and painting the Speed list
    ///     green-to-red would assert exactly that. Mix-invariant, and the band's word always
    ///     prints beside it (rule 8).
    /// </summary>
    public static string SpeedColor(int band) => $"var(--speed-{Math.Clamp(band, 0, 4) + 1})";

    /// <summary>
    ///     Variability token — how split a peer group is on a chart, five steps from very
    ///     consistent to very split (docs/design/pumbility-overhaul.md D35). Mix-invariant. The word
    ///     always prints beside it; the colour never carries the level alone.
    /// </summary>
    public static string VariabilityColor(PeerVariabilityLevel level) =>
        $"var(--vary-{MixThemes.VariabilityIndex(level)})";

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
