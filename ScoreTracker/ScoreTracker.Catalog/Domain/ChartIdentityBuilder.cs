using ScoreTracker.Catalog.Contracts;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Picks a chart's chips against its folder (docs/design/chart-identity.md §3). Pure: the
///     chart's banked profile plus the folder's baselines in, display-ordered chips out.
///     <para>
///         Claims STACK. This is not a ladder where the first match wins — a chart can be a
///         half-double AND twist-heavy AND drenched in close twists, and saying only the first
///         of those describes a different chart than the one in front of you. Identity is
///         uncapped for the same reason, and a chart that earns nothing gets nothing.
///     </para>
/// </summary>
internal static class ChartIdentityBuilder
{
    public static IReadOnlyList<IdentityChipRecord> Build(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        // Presence is measured coverage clearing the badge's own bar, and nothing else. A
        // dominance pick under that bar used to be admitted "because their pick is a real signal
        // about emphasis"; every chip the owner could not find on the pad came in through that
        // clause, and it crowded out real coverage through the cap besides.
        var present = profile.PresentBadges(folder)
            .Where(b => !ChartIdentityRules.IsBracketFamily(b) || profile.BracketsAreCredible)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var identity = new List<IdentityChipRecord>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1 — the shape of the body, which outranks anything about steps: it is the difference
        // between a half-double that features twists and a twist chart that features mid-6.
        if (Width(profile, folder) is { } width) identity.Add(width);
        if (Twist(profile, folder) is { } twist) identity.Add(twist);
        if (Speed(profile, folder) is { } speed) identity.Add(speed);

        // 2 — what almost nothing else here has. Rarest first, because the rarer it is the more
        // it is the reason someone is looking at this chart.
        foreach (var badge in present
                     // Whole-chart qualities never come through here. They carry no coverage, so
                     // "rare in this folder" collapses to "only this chart was picked for it",
                     // which is true of any pick and claims nothing: it is how Monolith's
                     // sustained pick — over ten seconds of tension — was calling itself the
                     // chart's identity. They have their own test below.
                     .Where(b => !ChartIdentityRules.IsWholeChartBadge(b))
                     .Where(b => folder.TryGetValue(b, out var baseline) && baseline.IsUniqueInFolder
                         && ClearsClaimBar(profile, folder, b))
                     .OrderBy(b => folder[b].PresentCount)
                     .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
                     .Take(ChartIdentityRules.MaxUniqueChips))
        {
            identity.Add(Chip(IdentityChipKind.Unique, IdentityTier.Identity, badge, Coverage(profile, badge)));
            claimed.Add(badge);
        }

        // 3 — what the chart is MADE of. Against the folder's p90 rather than a multiple of its
        // p75: twice the p75 sat above the folder's own maximum for 108 of 345 badge/folder
        // pairs, so a third of the vocabulary could never be claimed by anything at all.
        foreach (var badge in present
                     .Where(b => !claimed.Contains(b) && !ChartIdentityRules.IsPadGeographyBadge(b)
                                 && !ChartIdentityRules.IsWholeChartBadge(b))
                     .Where(b => folder.TryGetValue(b, out var baseline)
                                 && baseline.IsDrenched(profile.CoverageOf(b)))
                     .OrderByDescending(b => Margin(profile, folder, b))
                     .ThenBy(b => b, StringComparer.OrdinalIgnoreCase))
        {
            identity.Add(Chip(IdentityChipKind.Core, IdentityTier.Identity, badge, Coverage(profile, badge)));
            claimed.Add(badge);
        }

        // 4 — the chart is longer than it is hard. The pick alone is not enough: Monolith carries
        // a sustained pick over ten seconds of tension, which is nobody's idea of a grind.
        if (present.Contains("sustained") && IsExtremeSustain(profile, folder))
        {
            identity.Add(Chip(IdentityChipKind.Core, IdentityTier.Identity, "sustained", null));
            claimed.Add("sustained");
        }

        // 5 and 6 — the hardest stretch, described two ways. Spike is an ELEVATION claim (this
        // plays above its rating); the hard-section chip is a COMPOSITION claim (whatever the
        // hard part is, it is runs). Most charts are flat, have no spike, and still have a
        // hardest part — which is exactly the chart whose coda was invisible before.
        var peakiness = profile.CruxPeakiness;
        if (peakiness >= ChartIdentityRules.SpikePeakiness)
            identity.Add(new IdentityChipRecord(IdentityChipKind.Spike, IdentityTier.Identity,
                string.Empty, string.Empty, null, peakiness));

        if (HardSection(profile, claimed, present) is { } hardSection) identity.Add(hardSection);

        // 7 — UNION with piucenter's own picks (owner, 2026-08-26). Their chart_skill_summary is
        // the same idea as ours computed on better inputs: a percentile of each technique against
        // charts of the same type a level either side (piu-annotate, get_top_chart_skills). Where
        // they name something we did not, that is a second opinion worth carrying rather than a
        // contradiction — measured on Phoenix 2 D23, 77% of our claims are already theirs too, so
        // the union adds under half a badge per chart.
        // Their picks still admit nothing they should not: the bracket veto and the geography
        // rule both apply, because those exist to overrule a measurement we do not trust.
        foreach (var badge in profile.DominanceRank
                     .OrderBy(kv => kv.Value)
                     .Select(kv => kv.Key)
                     .Where(b => !claimed.Contains(b))
                     .Where(b => !ChartIdentityRules.IsPadGeographyBadge(b))
                     .Where(b => !ChartIdentityRules.IsBracketFamily(b) || profile.BracketsAreCredible)
                     // Sustained is the one pick with a measurement behind it that we trust more
                     // than the pick. Theirs is the variance of the eNPS timeline, which says the
                     // chart is EVEN, not that it is long — Monolith carries a sustained pick over
                     // ten seconds of tension, which is nobody's idea of a grind.
                     .Where(b => !b.Equals("sustained", StringComparison.OrdinalIgnoreCase)
                                 || IsExtremeSustain(profile, folder)))
        {
            identity.Add(Chip(IdentityChipKind.Core, IdentityTier.Identity, badge, Coverage(profile, badge)));
            claimed.Add(badge);
            present.Remove(badge);
        }

        // Everything else that cleared presence. Features are allowed to be ordinary — that was
        // only ever a problem while they shouted at the same volume as the claims above.
        var features = present
            .Where(b => !claimed.Contains(b))
            .OrderByDescending(b => Margin(profile, folder, b))
            .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
            .Select(b => Chip(IdentityChipKind.Core, IdentityTier.Feature, b, Coverage(profile, b)))
            .ToList();

        features.AddRange(PadGeographyFeatures(profile, folder));

        var chips = identity.Concat(features).ToList();

        // Nothing stood out anywhere AND piucenter picked nothing either — only reachable for a
        // chart whose analysis banked no summary at all.
        if (chips.Count == 0)
            chips.AddRange(profile.DominanceRank
                .OrderBy(kv => kv.Value)
                .Take(ChartIdentityRules.MaxFallbackChips)
                .Select(kv => Chip(IdentityChipKind.Fallback, IdentityTier.Feature, kv.Key, null)));

        return chips;
    }

    /// <summary>
    ///     How much of the pad the chart uses. The confined end is an absolute — a chart is
    ///     charted WITHIN a region or it is not, and Hymn of Golden Glory SC's 99.48% means it
    ///     steps outside twice, which is not never. The wide end is folder-relative because
    ///     every doubles chart is middle-heavy and "wide" only means anything here.
    /// </summary>
    private static IdentityChipRecord? Width(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        var mid4 = profile.GeometryOf(PiuCenterMetrics.PadShareMid4);
        var mid6 = profile.GeometryOf(PiuCenterMetrics.PadShareMid6);
        if (mid6 == null) return null;

        if (mid4 >= (decimal)ChartIdentityRules.WidthConfinedShare)
            return Geometry(IdentityChipKind.Width, WidthLabels.QuarterDouble, mid4.Value);
        if (mid6 >= (decimal)ChartIdentityRules.WidthConfinedShare)
            return Geometry(IdentityChipKind.Width, WidthLabels.HalfDouble, mid6.Value);

        return folder.TryGetValue(PiuCenterMetrics.PadShareMid6, out var baseline)
               && baseline.AnalyzedCharts > 0 && mid6 <= baseline.CoreCutoff
            ? Geometry(IdentityChipKind.Width, WidthLabels.Wide, mid6.Value)
            : null;
    }

    /// <summary>
    ///     How far the chart turns you. The crossed guard is not decoration: a chart can be quiet
    ///     on side-on stances and still cross your feet hard the few times it moves, and calling
    ///     that twistless would be the measure lying about its one job.
    /// </summary>
    private static IdentityChipRecord? Twist(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        if (profile.GeometryOf(PiuCenterMetrics.StanceSideOn) is not { } sideOn) return null;
        var crossed = profile.GeometryOf(PiuCenterMetrics.StanceCrossed) ?? 0m;
        var isDoubles = profile.GeometryOf(PiuCenterMetrics.PadShareMid6) != null;

        if (sideOn <= (decimal)ChartIdentityRules.TwistlessShare(isDoubles)
            && crossed <= (decimal)ChartIdentityRules.TwistlessMaximumCrossed
            && IsSquareToTheScreen(profile, folder))
            return Geometry(IdentityChipKind.Twist, WidthLabels.Twistless, sideOn);

        return folder.TryGetValue(PiuCenterMetrics.StanceSideOn, out var baseline)
               && baseline.AnalyzedCharts > 0 && baseline.DrenchedCutoff > 0
               && sideOn >= baseline.DrenchedCutoff
            ? Geometry(IdentityChipKind.Twist, WidthLabels.TwistHeavy, sideOn)
            : null;
    }

    /// <summary>
    ///     Whether the chart really leaves the body facing the screen. Side-on share alone does
    ///     not answer it: a side-3 passage played with a foot on the centre panel puts the feet on
    ///     a 45° line, which registers as no side-on stance at all — HEART RABBIT COASTER S21
    ///     measures 4.4% side-on and 85% diagonal, and calling that twistless is the measure lying
    ///     about the one thing it is for. Folder-relative because the diagonal share's median says
    ///     nothing (~78% everywhere) while its low tail separates cleanly.
    /// </summary>
    private static bool IsSquareToTheScreen(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        if (profile.GeometryOf(PiuCenterMetrics.StanceDiagonal) is not { } diagonal) return true;
        return !folder.TryGetValue(PiuCenterMetrics.StanceDiagonal, out var baseline)
               || baseline.CoreCutoff <= 0
               || diagonal <= baseline.CoreCutoff;
    }

    /// <summary>
    ///     Speed only claims a chart at the extremes. "Mid Tempo" is a measurement, not a claim,
    ///     and the outer bands have to keep meaning what they say — so this is the Speed list's
    ///     own boundary and nothing softer.
    /// </summary>
    private static IdentityChipRecord? Speed(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        if (profile.GeometryOf(PiuCenterMetrics.Nps) is not { } nps || nps <= 0) return null;
        if (!folder.TryGetValue(PiuCenterMetrics.Nps, out var baseline) || baseline.DrenchedCutoff <= 0) return null;
        if (nps >= baseline.DrenchedCutoff)
            return Geometry(IdentityChipKind.Speed, WidthLabels.VeryFast, nps);
        return nps <= baseline.CoreCutoff
            ? Geometry(IdentityChipKind.Speed, WidthLabels.VerySlow, nps)
            : null;
    }

    /// <summary>
    ///     One chip for the hardest stretch, carrying its length and up to two badges. Never two
    ///     chips: it is one window, so a second would print the same duration again.
    /// </summary>
    private static IdentityChipRecord? HardSection(ChartBadgeProfile profile, ISet<string> claimed,
        ISet<string> present)
    {
        if (profile.CruxPeakiness is not { } peakiness
            || peakiness < ChartIdentityRules.HardSectionFeaturePeakiness) return null;

        var badges = profile.CruxBadges
            // Pad geography is the width chip's business. Left in, Burn Out's crux — which ranks
            // mid-4 second — resurrects exactly the chip the owner rejected on that chart.
            .Where(b => !ChartIdentityRules.IsPadGeographyBadge(b))
            .Where(b => !ChartIdentityRules.IsBracketFamily(b) || profile.BracketsAreCredible)
            // A badge already claimed above is not news again down here.
            .Where(b => !claimed.Contains(b))
            .Take(ChartIdentityRules.MaxHardSectionBadges)
            .Select(b => new IdentityChipBadge(b, BadgeLabels.DisplayName(b), BadgeLabels.CategoryFor(b)))
            .ToArray();
        if (badges.Length == 0) return null;

        foreach (var badge in badges) present.Remove(badge.Badge);

        return new IdentityChipRecord(IdentityChipKind.HardSection,
            peakiness >= ChartIdentityRules.HardSectionIdentityPeakiness
                ? IdentityTier.Identity
                : IdentityTier.Feature,
            string.Empty, string.Empty, null, profile.CruxDuration, badges);
    }

    /// <summary>
    ///     Where the chart stands, as a feature and never a claim — width owns the claim. Gated
    ///     on measured note share rather than segment coverage, which is what finally squares
    ///     the owner's verdicts: Burn Out's segment-derived "71% mid-4" is 68% by actual notes
    ///     against a folder p75 of 72%, so the chip he could not find on the pad is gone, while
    ///     the two he endorsed are comfortably over their own folders' bars.
    /// </summary>
    private static IEnumerable<IdentityChipRecord> PadGeographyFeatures(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        foreach (var (metric, badge) in new[]
                 {
                     (PiuCenterMetrics.PadShareMid6, "mid6_doubles"),
                     (PiuCenterMetrics.PadShareMid4, "mid4_doubles")
                 })
        {
            if (profile.GeometryOf(metric) is not { } share) continue;
            if (!folder.TryGetValue(metric, out var baseline) || baseline.DrenchedCutoff <= 0) continue;
            if (share < baseline.DrenchedCutoff) continue;
            yield return Chip(IdentityChipKind.Core, IdentityTier.Feature, badge, share);
        }
    }

    private static bool IsExtremeSustain(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        return profile.GeometryOf(PiuCenterMetrics.TimeUnderTension) is { } tension
               && folder.TryGetValue(PiuCenterMetrics.TimeUnderTension, out var baseline)
               && baseline.DrenchedCutoff > 0
               && tension >= baseline.DrenchedCutoff;
    }

    private static bool ClearsClaimBar(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder, string badge)
    {
        return ChartIdentityRules.IsWholeChartBadge(badge)
               || (folder.TryGetValue(badge, out var baseline)
                   && profile.CoverageOf(badge) >= baseline.ClaimCoverage);
    }

    /// <summary>
    ///     How far a badge's coverage stands above what the folder asks of it. Whole-chart
    ///     qualities have no coverage, so they sort last among their group rather than borrowing
    ///     a number they do not have.
    /// </summary>
    private static decimal Margin(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder, string badge)
    {
        if (ChartIdentityRules.IsWholeChartBadge(badge)) return 0m;
        return profile.CoverageOf(badge) - (folder.TryGetValue(badge, out var baseline) ? baseline.CoreCutoff : 0m);
    }

    private static decimal? Coverage(ChartBadgeProfile profile, string badge)
    {
        return ChartIdentityRules.IsWholeChartBadge(badge) ? null : profile.CoverageOf(badge);
    }

    private static IdentityChipRecord Chip(IdentityChipKind kind, IdentityTier tier, string badge, decimal? detail)
    {
        return new IdentityChipRecord(kind, tier, badge, BadgeLabels.DisplayName(badge),
            BadgeLabels.CategoryFor(badge), detail);
    }

    private static IdentityChipRecord Geometry(IdentityChipKind kind, string label, decimal detail)
    {
        return new IdentityChipRecord(kind, IdentityTier.Identity, label, label, null, detail);
    }
}

/// <summary>
///     The geometry claims' badge keys. They are not piucenter badges and belong to no family —
///     a chart's shape is not one of its skills, the same reason the spike wears no family
///     colour — but they travel as chips, so they need stable keys the UI can localize.
/// </summary>
internal static class WidthLabels
{
    public const string QuarterDouble = "Quarter Double";
    public const string HalfDouble = "Half-Double";
    public const string Wide = "Wide";
    public const string Twistless = "Twistless";
    public const string TwistHeavy = "Twist-heavy";
    public const string VeryFast = "Very Fast";
    public const string VerySlow = "Very Slow";

    /// <summary>Whether a chip key is one of these shape claims rather than a piucenter badge.</summary>
    public static bool IsGeometryClaim(string badge)
    {
        return badge is QuarterDouble or HalfDouble or Wide or Twistless or TwistHeavy
            or VeryFast or VerySlow;
    }
}
