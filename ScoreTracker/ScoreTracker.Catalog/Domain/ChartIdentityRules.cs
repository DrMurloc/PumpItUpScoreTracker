namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The one place the chart-identity policy's numbers live
///     (docs/design/chart-identity.md §3). Every one of these is owner-tunable and was
///     validated against real folders — change them here and every surface moves together.
/// </summary>
internal static class ChartIdentityRules
{
    /// <summary>
    ///     How much of a folder one technique may claim, before rarity is taken into account.
    ///     The bar for a badge is set so roughly <c>PresenceBudget ÷ prevalence</c> of the
    ///     folder can clear it — the rarer a technique is here, the less of it a chart needs
    ///     before having it is worth saying, and the more common it is, the more dominant a
    ///     chart has to be before it may claim it at all.
    ///     <para>
    ///         This replaces a fixed 0.30 bar with a hand-tuned table of raised values, and it
    ///         replaces them because a fixed bar reads two very different facts identically.
    ///         Brackets sit on 13.7% of Phoenix 2 S14 and 79.4% of D26: at S14 the bar was above
    ///         the whole folder and not one chart could say it had brackets, while at D26 a run
    ///         chart with a handful of them could. Owner, 2026-08-26: "A d26 with a handful of
    ///         brackets but overall just being a run shouldn't even mention the thought of
    ///         brackets. A S18 with brackets probably should at least feature them."
    ///     </para>
    ///     <para>
    ///         The old raised bars fall out of this rather than being configured: jack, jump,
    ///         run and twist_90 were raised by hand because they ride nearly every chart, and
    ///         their prevalence — stable at 56–78% in every folder — now says so on its own.
    ///     </para>
    /// </summary>
    public const double PresenceBudget = 0.10;

    /// <summary>
    ///     The bar for a chart in a folder we have no baseline for. Only reachable before a
    ///     folder has been swept; a real answer always comes from the baseline.
    /// </summary>
    public const decimal FallbackQualifyingCoverage = 0.30m;

    /// <summary>
    ///     Badges that describe the whole chart rather than a stretch of it. They are never
    ///     banked with a coverage — a null there reads as "this is true of the chart", not
    ///     "zero percent" — so presence for these comes from the dominance pick alone, and
    ///     they are never asked to clear a percentile.
    /// </summary>
    private static readonly IReadOnlySet<string> WholeChartBadges =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bursty", "sustained",
            // Piucenter's two summary qualities, added 2026-08-26. They are chart-level only —
            // their own pipeline bans run_without_twists from segment badges outright — so they
            // carry no coverage anywhere, and asking them for one silently dropped them from
            // 2,190 charts they had been picked for.
            "twists", "run_without_twists"
        };

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

    /// <summary>
    ///     How much of a chart may be played on a diagonal before "twistless" is a lie. Side-on
    ///     share alone is not enough: side-3 passages with a foot on the centre panel put the feet
    ///     on a 45° line and register almost no side-on stance at all, so HEART RABBIT COASTER S21
    ///     measured 4.4% side-on — and 85% diagonal — while being anything but twistless. A
    ///     folder percentile rather than a fixed number, because the diagonal share's MEDIAN is
    ///     uninformative (~78% at every level) while its low tail discriminates cleanly.
    /// </summary>
    public const double TwistlessDiagonalQuantile = 0.10;

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

    /// <summary>
    ///     What share of a folder may clear a badge's presence bar, given how many of its charts
    ///     carry the badge at all. Capped at the prevalence itself: a technique on 8% of a folder
    ///     cannot be claimed by 12% of it, so there the bar falls to "has any at all", which is
    ///     the whole fix for the rarest techniques: their folder MAXIMUMS sit below the old fixed
    ///     bar, so no chart anywhere could say it carried one.
    /// </summary>
    public static double AllowedShare(double prevalence)
    {
        return prevalence <= 0 ? 0 : Math.Min(prevalence, PresenceBudget / prevalence);
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
        return GeographyBadges.Contains(badge);
    }

    /// <summary>
    ///     Where the chart puts you, rather than what it asks you to do. side3_singles is the
    ///     singles counterpart of the doubles mid-4/mid-6 pair — it says the chart confines you
    ///     to one side of the pad, which is a position and not a technique, and it should not
    ///     compete with jumps and jacks for the card (owner, 2026-08-26).
    /// </summary>
    private static readonly IReadOnlySet<string> GeographyBadges =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "mid4_doubles", "mid6_doubles", "side3_singles" };

    public static double TwistlessShare(bool isDoubles)
    {
        return isDoubles ? TwistlessShareDoubles : TwistlessShareSingles;
    }
}
