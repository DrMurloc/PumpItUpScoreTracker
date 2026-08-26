namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The one place the chart-identity policy's numbers live
///     (docs/design/chart-identity.md §3). Every one of these is owner-tunable and was
///     validated against real folders — change them here and every surface moves together.
/// </summary>
internal static class ChartIdentityRules
{
    /// <summary>
    ///     What it takes for a badge to count as really being on a chart. Piucenter's
    ///     dominance summary is a ranking, not a measurement: it names a chart's top three
    ///     badges however little of the chart they ride. Presence is measured coverage
    ///     clearing this bar, so a chart with a #3 pick it barely carries is not that kind
    ///     of chart — the rule Achluoias D24 earned, where a bracket_drill pick over 12.5%
    ///     measured brackets had been filing a run chart under Brackets.
    /// </summary>
    private const decimal DefaultQualifyingCoverage = 0.30m;

    /// <summary>
    ///     Badges that ride nearly every chart need a higher bar, or one of them swallows a
    ///     third of a folder on coverage alone. Calibrated 2026-07-11 against the full 050726
    ///     corpus; carried over from the deleted skill mapper, which is where they were born.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> RaisedQualifyingCoverage =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["jack"] = 0.40m,
            ["jump"] = 0.50m,
            ["run"] = 0.40m,
            ["twist_90"] = 0.40m
        };

    /// <summary>
    ///     Badges that describe the whole chart rather than a stretch of it. They are never
    ///     banked with a coverage — a null there reads as "this is true of the chart", not
    ///     "zero percent" — so presence for these comes from the dominance pick alone, and
    ///     they are never asked to clear a percentile.
    /// </summary>
    private static readonly IReadOnlySet<string> WholeChartBadges =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bursty", "sustained" };

    /// <summary>
    ///     Piucenter's bracket detection is a limb-assignment model, and it reads an ordinary
    ///     jump as a bracket often enough to invent a bracket chart out of one that has none:
    ///     Heliosphere D20 carries eleven bracket rows in 845, every one a centre-plus-adjacent
    ///     pair, five of them clustered in the final section — which is exactly why it wore a
    ///     bracket-jump badge that nobody watching the chart could find. Anything in this family
    ///     has to clear <see cref="MinimumBracketRowShare" /> measured off the arrows before it
    ///     may become any kind of chip.
    /// </summary>
    private static readonly IReadOnlySet<string> BracketFamilyBadges =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bracket", "bracket_run", "bracket_drill", "bracket_jump", "bracket_twist", "staggered_bracket"
        };

    /// <summary>Where a badge's coverage has to land in its folder to read as core.</summary>
    public const double CoreQuantile = 0.75;

    /// <summary>
    ///     Where a badge's coverage has to land to read as the chart being DRENCHED in it.
    ///     <para>
    ///         This was a multiple of the core cutoff (2x) and that was unsatisfiable for 108 of
    ///         345 badge/folder pairs, because twice a folder's 75th percentile routinely sits
    ///         above the folder's own maximum. Doublestep in Phoenix 2 D20 runs p75 .375 against
    ///         a folder maximum of .714, so the rule demanded .727 and no chart could ever pass:
    ///         Nakakapagpabagabag sits at exactly the folder maximum and was still denied. A
    ///         percentile always exists and always scales to the badge.
    ///     </para>
    /// </summary>
    public const double DrenchedQuantile = 0.90;

    /// <summary>
    ///     The floor under the percentile. Most badges are absent from most charts, so a
    ///     folder's 75th-percentile coverage for a rare badge is zero — without this, every
    ///     chart in the folder would clear it and the chip would say nothing.
    /// </summary>
    public const decimal CoreCoverageFloor = 0.15m;

    /// <summary>How much of a folder may carry a badge before it stops being remarkable.</summary>
    public const double UniquePrevalence = 0.12;

    /// <summary>
    ///     How far past its own bar a badge must reach before it may claim the chart, as a
    ///     multiple of that bar. Coverage moves in steps of one segment — a seven-segment chart
    ///     jumps by .143 — so without a margin "just over the bar" and "far over it" are the
    ///     same reading. That Kitty D22 cleared jack's .40 bar by .029, and because jacks are
    ///     rare in that folder the pass promoted it to the loudest chip on the card.
    ///     <para>
    ///         Applies to the drenched claim too, not only the rare one: a folder's p90 for a
    ///         rare badge is low BECAUSE it is rare, and rarity must not lower the bar.
    ///     </para>
    /// </summary>
    public const decimal ClaimMarginMultiple = 1.25m;

    /// <summary>How far a crux must run over the printed level to read as a spike.</summary>
    public const decimal SpikePeakiness = 0.7m;

    /// <summary>
    ///     The two gates under the spike. A hard section is worth naming well before it is worth
    ///     calling a spike — most charts are flat and still have a hardest stretch, which is the
    ///     whole reason New Rose's twenty-three-second coda was invisible. Calibrated against the
    ///     owner's own reports: That Kitty (.17) stays silent, Windmill (.21) stays clean, New
    ///     Rose (.29) speaks as a feature, BSPower (.62) headlines.
    /// </summary>
    public const decimal HardSectionIdentityPeakiness = 0.5m;

    public const decimal HardSectionFeaturePeakiness = 0.25m;

    /// <summary>
    ///     Note-share on the middle four or middle six panels that reads as the chart never
    ///     leaving them. Deliberately absolute rather than folder-relative: a claim that a chart
    ///     is charted WITHIN a region is a structural fact about it, not a comparison. Hymn of
    ///     Golden Glory SC D20 measures 99.48% and is therefore not a half-double — it steps
    ///     outside twice, and twice is not never.
    /// </summary>
    public const double WidthConfinedShare = 0.995;

    /// <summary>
    ///     Where a chart's middle-six share has to fall to read as using the whole pad. Every
    ///     doubles chart is middle-heavy — the outer corners are structurally rare, so the whole
    ///     population lives between roughly 70% and 100% — which is why this end is a folder
    ///     percentile and the confined end is not.
    /// </summary>
    public const double WideQuantile = 0.10;

    /// <summary>Where geography stops being folder-normal and becomes worth printing.</summary>
    public const double PadShareFeatureQuantile = 0.75;

    /// <summary>
    ///     Side-on stance share that reads as a chart that barely turns you. Doubles sit higher
    ///     because travelling between pads passes through side-on stances even when nothing about
    ///     the chart is twisty.
    /// </summary>
    public const double TwistlessShareSingles = 0.05;

    public const double TwistlessShareDoubles = 0.10;

    /// <summary>
    ///     A chart may be quiet on side-on stances and still cross your feet hard when it moves.
    ///     Vook D20 measures 8.8% side-on of which 7.8% is crossovers; calling that twistless
    ///     would be the measure lying about the one thing it is for.
    /// </summary>
    public const double TwistlessMaximumCrossed = 0.02;

    /// <summary>Where a folder's side-on share stops being ordinary and becomes the chart's point.</summary>
    public const double TwistHeavyQuantile = 0.90;

    /// <summary>
    ///     How much of a chart has to actually bracket before its bracket badges are believed.
    ///     Calibrated against the owner's own field verdicts, which the measure separates
    ///     cleanly: Nakaka 6.31%, STAGER 5.65% and Windmill 4.29% were all "nailed it", while
    ///     BSPower 2.99%, Heliosphere 1.30% and 4NT 0.56% were all "there's like none".
    /// </summary>
    public const double MinimumBracketRowShare = 0.03;

    /// <summary>
    ///     How far from the folder's mean speed a chart has to sit before speed is its identity
    ///     rather than a measurement. This is exactly the Speed list's own outer band boundary,
    ///     so no new number enters the system. Not softened to catch near-misses (owner,
    ///     2026-08-26): A Site De La Rue D20 sits at z = 1.46 and does not qualify, because the
    ///     Very bands have to mean what they say.
    /// </summary>
    public const double SpeedIdentityZ = 1.5;

    public const int MaxUniqueChips = 2;
    public const int MaxCoreChips = 3;

    /// <summary>Badges carried by the one merged hard-section chip.</summary>
    public const int MaxHardSectionBadges = 2;

    public const int MaxFallbackChips = 3;

    public static decimal QualifyingCoverage(string badge)
    {
        return RaisedQualifyingCoverage.TryGetValue(badge, out var raised) ? raised : DefaultQualifyingCoverage;
    }

    /// <summary>The bar a badge must clear to claim the chart, rather than merely be present on it.</summary>
    public static decimal ClaimCoverage(string badge)
    {
        return QualifyingCoverage(badge) * ClaimMarginMultiple;
    }

    public static bool IsWholeChartBadge(string badge)
    {
        return WholeChartBadges.Contains(badge);
    }

    public static bool IsBracketFamily(string badge)
    {
        return BracketFamilyBadges.Contains(badge);
    }

    /// <summary>
    ///     Geography is the width chip's business. Left admissible as an ordinary chip it also
    ///     comes back through the hard section — Burn Out's crux ranks mid-4 second, which would
    ///     resurrect exactly the chip the owner rejected on that chart.
    /// </summary>
    public static bool IsPadGeographyBadge(string badge)
    {
        return badge.Equals("mid4_doubles", StringComparison.OrdinalIgnoreCase)
               || badge.Equals("mid6_doubles", StringComparison.OrdinalIgnoreCase);
    }

    public static double TwistlessShare(bool isDoubles)
    {
        return isDoubles ? TwistlessShareDoubles : TwistlessShareSingles;
    }
}
