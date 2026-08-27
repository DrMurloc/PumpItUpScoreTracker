using Microsoft.Extensions.Localization;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Web.Components;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Renders a chart's identity chips (docs/design/chart-identity.md §3) into the card
///     vocabulary every chart surface already speaks. One place, because the tier lists, the
///     chart page and its dialog all show the same chips and a kind that reads differently on
///     one of them would say the chart is different there.
///     <para>
///         Badge names stay English in every locale — the long-standing ruling for the pattern
///         vocabulary — but still travel through the localizer so translating them later is a
///         resx change rather than a code change. The words around them ("Hardest {0}s") are
///         real UI copy and localize normally.
///     </para>
/// </summary>
public static class IdentityChips
{
    /// <param name="showCoverage">
    ///     The "Show Skill Metric" preference. Coverage is off by default: the number is
    ///     meaningless without the folder around it, which is what the chip selection already
    ///     accounts for.
    /// </param>
    public static IReadOnlyList<TierListChartCard.CardSkillChip> ToCardChips(ChartIdentityRecord? identity,
        bool showCoverage, IStringLocalizer localizer)
    {
        if (identity == null || identity.Chips.Count == 0)
            return Array.Empty<TierListChartCard.CardSkillChip>();

        return identity.Chips.Select(chip => ToChip(chip, showCoverage, localizer)).ToArray();
    }

    private static TierListChartCard.CardSkillChip ToChip(IdentityChipRecord chip, bool showCoverage,
        IStringLocalizer localizer)
    {
        var identity = chip.Tier == IdentityTier.Identity;
        switch (chip.Kind)
        {
            // The spike is a shape, not a skill: no badge, no family colour. Named in full
            // rather than as an arrow and a number, which said nothing to anyone who had not
            // been told what it meant (owner, 2026-08-26).
            case IdentityChipKind.Spike:
                return new TierListChartCard.CardSkillChip(
                    localizer["Difficulty Spike"].Value, "chip-spike",
                    chip.Detail == null ? null : Signed(chip.Detail.Value), identity);

            // One chip, both badges: it is one window, so a second chip would print the same
            // duration twice. The body carries no family colour because it names a STRETCH
            // rather than a skill — each badge inside keeps its own.
            case IdentityChipKind.HardSection:
                return new TierListChartCard.CardSkillChip(
                    localizer["Hardest {0}s:", Seconds(chip.Detail)].Value, "chip-section", null, identity,
                    (chip.Badges ?? Array.Empty<IdentityChipBadge>())
                    .Select(b => new TierListChartCard.CardSkillChipPart(
                        localizer[b.DisplayName].Value, BadgeCategoryClasses.For(b.Family)))
                    .ToArray());

            // Their longest unbroken run, named in seconds because that is the thing a player
            // feels — "Longest run: 22s" says more than any share of the chart could. Wears the
            // Stamina & Runs family rather than the geometry hue (owner, 2026-08-26): a chart's
            // longest run IS a stamina claim, and colouring it apart said it was something else.
            case IdentityChipKind.LongestRun:
                return new TierListChartCard.CardSkillChip(
                    localizer["Longest run: {0}s", Seconds(chip.Detail)].Value,
                    BadgeCategoryClasses.For(BadgeCategory.StaminaAndRuns), null, identity);

            // How much pad the chart uses. Doubles Tech green, for the same reason Longest run
            // is red: a width claim only ever fires on a doubles chart, and where you stand on
            // the pad is what that family is about (owner, 2026-08-26).
            case IdentityChipKind.Width:
                return new TierListChartCard.CardSkillChip(
                    localizer[chip.DisplayName].Value,
                    BadgeCategoryClasses.For(BadgeCategory.DoublesTech), null, identity);

            // Its own five-stop ramp, cool to hot, matching the Speed list's section headers so
            // the chip and the folder it came from cannot disagree. Only the outer bands ever
            // reach a chip, so only the outer stops are used.
            case IdentityChipKind.Speed:
                return new TierListChartCard.CardSkillChip(
                    localizer[chip.DisplayName].Value,
                    chip.Badge == IdentityClaimKeys.VeryFast ? "chip-speed-fast" : "chip-speed-slow",
                    null, identity);

            // The shape of the body — its own hue, outside the five families, for the same
            // reason the spike is: how far a chart turns you is not one of its skills.
            case IdentityChipKind.Twist:
                return new TierListChartCard.CardSkillChip(
                    localizer[chip.DisplayName].Value, "chip-geometry", null, identity);

            // ✦ marks the rare one. A dashed border alone said "this chip is different" without
            // saying how, which is no message at all; the glyph carries it now and the border is
            // ordinary again (owner, 2026-08-26).
            case IdentityChipKind.Unique:
                return new TierListChartCard.CardSkillChip(
                    $"✦ {localizer[chip.DisplayName].Value}", ClassFor(chip),
                    showCoverage && chip.Detail != null ? Percent(chip.Detail.Value) : null, identity);

            default:
                return new TierListChartCard.CardSkillChip(
                    localizer[chip.DisplayName].Value, ClassFor(chip),
                    showCoverage && chip.Detail != null ? Percent(chip.Detail.Value) : null, identity);
        }
    }

    /// <summary>
    ///     The archived hand tags, as the Chabala lens shows them: neutral, uncoloured, mapped
    ///     to nothing. Tinting these would file the retired vocabulary under the badge families,
    ///     which is the association the rollup's removal exists to end
    ///     (docs/design/nuke-old-skill-categories.md §7).
    /// </summary>
    public static IReadOnlyList<TierListChartCard.CardSkillChip> ToArchivedChips(IReadOnlyList<string>? tags)
    {
        return tags == null
            ? Array.Empty<TierListChartCard.CardSkillChip>()
            : tags.Select(tag => new TierListChartCard.CardSkillChip(tag, string.Empty, null)).ToArray();
    }

    private static string ClassFor(IdentityChipRecord chip)
    {
        var family = BadgeCategoryClasses.For(chip.Family);
        var kind = chip.Kind switch
        {
            IdentityChipKind.Unique => "chip-unique",
            IdentityChipKind.Fallback => "chip-fallback",
            _ => string.Empty
        };
        return string.Join(' ', new[] { family, kind }.Where(c => c.Length > 0));
    }

    private static string Percent(decimal coverage)
    {
        return $"{(int)Math.Round(coverage * 100)}%";
    }

    /// <summary>
    ///     The stretch's length, whole seconds. The precision is spurious past that — it is a
    ///     segment boundary, not a stopwatch — and the number's job is to separate a six-second
    ///     stumble from a twenty-three-second ordeal.
    /// </summary>
    private static int Seconds(decimal? duration)
    {
        return duration == null ? 0 : (int)Math.Round(duration.Value);
    }

    /// <summary>Peakiness reads as a signed level offset — the sign is the whole message.</summary>
    private static string Signed(decimal peakiness)
    {
        return peakiness >= 0 ? $"+{peakiness:0.#}" : $"{peakiness:0.#}";
    }
}
