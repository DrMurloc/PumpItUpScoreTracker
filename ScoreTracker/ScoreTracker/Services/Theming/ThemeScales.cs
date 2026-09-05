using ScoreTracker.Domain.Models;
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
    ///     The step-chart strip's panel token for a lane (lane = panel % 5): the classic skin's
    ///     upper-red / lower-blue / center-yellow, mix-invariant
    ///     (docs/design/step-chart-failure-map.md D12).
    /// </summary>
    public static string StepPanelColor(int panel) => (panel % 5) switch
    {
        2 => "var(--panel-center)",
        1 or 3 => "var(--panel-upper)",
        _ => "var(--panel-lower)"
    };

    /// <summary>Feet mode's pair — the snapshot's own limb prediction, teal left / pink right.</summary>
    public static string FootColor(bool isLeft) => isLeft ? "var(--foot-l)" : "var(--foot-r)";

    /// <summary>Timing mode's DDR-style quantization token; 0 / unmodeled grids read as "other".</summary>
    public static string QuantColor(int quant) => quant switch
    {
        4 => "var(--quant-4)",
        8 => "var(--quant-8)",
        12 => "var(--quant-12)",
        16 => "var(--quant-16)",
        _ => "var(--quant-other)"
    };

    /// <summary>The failure rail's proven-Pass pin. The life pin is <see cref="LifeDanger" />.</summary>
    public static string StepPassPin => "var(--step-pass)";

    /// <summary>The walk-off pin — the AFK guard's 51-miss wall, not a death (D18).</summary>
    public static string StepWalkOff => "var(--step-walkoff)";

    /// <summary>The viewer's own broken runs on the rail.</summary>
    public static string StepYou => "var(--step-you)";

    /// <summary>
    ///     How fast a chart is FOR ITS FOLDER — five bands, slowest (0) to fastest (4)
    ///     (docs/design/chart-identity.md §2). Its own ramp, deliberately not the difficulty
    ///     one: a slow chart at a high level is not an easy one, and painting the Speed list
    ///     green-to-red would assert exactly that. Mix-invariant, and the band's word always
    ///     prints beside it (rule 8).
    /// </summary>
    public static string SpeedColor(int band) => $"var(--speed-{Math.Clamp(band, 0, 4) + 1})";

    /// <summary>The one glow a lit score wears (peers-abstraction.md D15): a threshold, not a spectrum.</summary>
    public const string ScoreGlowClass = "rarity-glow-2";

    /// <summary>
    ///     How a player's OWN score is painted, from the peers they chose and the color system and
    ///     glow rule they picked (docs/design/peers-abstraction.md D14–D16). The single place the
    ///     nine systems' cutoffs and the glow rule live; every surface renders through
    ///     <c>PeerScore</c>, which calls this. A system that reads the standing paints nothing when
    ///     no peer has passed the chart — plain ink, the popover says why — while the two that do
    ///     not (the actual grade, none) never look at it.
    /// </summary>
    public static ScoreStyle ScoreStyleFor(PeerStanding? standing, bool isPerfectGame, PhoenixLetterGrade? grade,
        ScoreColorSettings settings)
    {
        var measured = standing is { HasCohort: true };
        var percentile = measured ? standing!.Percentile!.Value : 0;
        var token = settings.System switch
        {
            ScoreColorSystem.JudgementSpectrum => measured ? CssVar(BandFor(percentile)) : string.Empty,
            ScoreColorSystem.Classic => measured ? ClassicToken(percentile) : string.Empty,
            ScoreColorSystem.GradeMetals => measured ? GradeMetalToken(percentile) : string.Empty,
            ScoreColorSystem.Podium => measured ? PodiumToken(standing!.Place) : string.Empty,
            ScoreColorSystem.SingleHue => measured ? HueToken(percentile) : string.Empty,
            ScoreColorSystem.ResultScreen => measured ? ResultScreenToken(percentile) : string.Empty,
            ScoreColorSystem.ThreeSteps => measured ? ThreeStepToken(percentile) : string.Empty,
            ScoreColorSystem.ActualGrade => grade is { } actual ? GradeColor(actual) : string.Empty,
            _ => string.Empty
        };

        // A Perfect Game is the ceiling: it is inside any top-N rule whether or not a peer has
        // passed the chart, and it is exactly what the Perfect Games rule names. Off is off.
        var lit = settings.Glow switch
        {
            GlowRule.PerfectGames => isPerfectGame,
            GlowRule.TopPlaces => isPerfectGame || (measured && standing!.Place <= settings.GlowThreshold),
            GlowRule.TopPercent => isPerfectGame ||
                                   (measured && percentile >= 1 - settings.GlowThreshold / 100.0),
            _ => false
        };

        return new ScoreStyle(token.Length == 0 ? string.Empty : $"color:{token};",
            lit ? ScoreGlowClass : string.Empty);
    }

    /// <summary>The classic ladder's seven rungs at 10 / 25 / 50 / 75 / 90 / 99 %.</summary>
    private static string ClassicToken(double percentile) => percentile switch
    {
        < .10 => "var(--classic-1)",
        < .25 => "var(--classic-2)",
        < .50 => "var(--classic-3)",
        < .75 => "var(--classic-4)",
        < .90 => "var(--classic-5)",
        < .99 => "var(--classic-6)",
        _ => "var(--classic-7)"
    };

    /// <summary>The grades' own metals by standing: below-A green up to the SSS+ ice at the top 1%.</summary>
    private static string GradeMetalToken(double percentile) => percentile switch
    {
        < .25 => "var(--grade-sub-a)",
        < .50 => "var(--plate-fg)",
        < .75 => "var(--plate-mg)",
        < .90 => "var(--plate-eg)",
        < .99 => "var(--plate-ug)",
        _ => "var(--plate-pg)"
    };

    /// <summary>Medals for a place: gold, silver, copper, then plain ink.</summary>
    private static string PodiumToken(int place) => place switch
    {
        1 => "var(--plate-sg)",
        2 => "var(--plate-mg)",
        3 => "var(--plate-fg)",
        _ => string.Empty
    };

    private static string HueToken(double percentile) => percentile switch
    {
        < .25 => "var(--hue-1)",
        < .50 => "var(--hue-2)",
        < .75 => "var(--hue-3)",
        < .90 => "var(--hue-4)",
        < .99 => "var(--hue-5)",
        _ => "var(--hue-6)"
    };

    /// <summary>The judgement colors, literally: the one ladder that starts red, so opt-in only.</summary>
    private static string ResultScreenToken(double percentile) => percentile switch
    {
        < .25 => JudgmentColor(Judgment.Miss),
        < .50 => JudgmentColor(Judgment.Bad),
        < .75 => JudgmentColor(Judgment.Good),
        < .90 => JudgmentColor(Judgment.Great),
        _ => JudgmentColor(Judgment.Perfect)
    };

    private static string ThreeStepToken(double percentile) => percentile switch
    {
        < .50 => string.Empty,
        < .90 => "var(--rarity-gold)",
        _ => "var(--rarity-sapphire)"
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

/// <summary>What <see cref="ThemeScales.ScoreStyleFor" /> hands a surface: an inline color fragment (or none) and the glow class (or none).</summary>
public readonly record struct ScoreStyle(string Style, string GlowClass)
{
    public static ScoreStyle Plain { get; } = new(string.Empty, string.Empty);
}
