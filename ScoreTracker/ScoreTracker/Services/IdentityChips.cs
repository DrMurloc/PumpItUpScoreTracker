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
///         resx change rather than a code change. The kind labels around them ("Spike") are
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

        return identity.Chips.Select(chip => chip.Kind switch
        {
            // The spike is a shape, not a skill: no badge, no family colour, and the number
            // IS the chip rather than an annotation on one.
            IdentityChipKind.Spike => new TierListChartCard.CardSkillChip(
                localizer["Spike"].Value, "chip-spike",
                chip.Detail == null ? null : Signed(chip.Detail.Value)),
            IdentityChipKind.Crux => new TierListChartCard.CardSkillChip(
                localizer["crux: {0}", Label(chip, localizer)].Value, ClassFor(chip), null),
            _ => new TierListChartCard.CardSkillChip(Label(chip, localizer), ClassFor(chip),
                showCoverage && chip.Detail != null ? Percent(chip.Detail.Value) : null)
        }).ToArray();
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

    private static string Label(IdentityChipRecord chip, IStringLocalizer localizer)
    {
        return localizer[chip.DisplayName].Value;
    }

    private static string ClassFor(IdentityChipRecord chip)
    {
        var family = BadgeCategoryClasses.For(chip.Family);
        var kind = chip.Kind switch
        {
            IdentityChipKind.Unique => "chip-unique",
            IdentityChipKind.Crux => "chip-crux",
            IdentityChipKind.Fallback => "chip-fallback",
            _ => string.Empty
        };
        return string.Join(' ', new[] { family, kind }.Where(c => c.Length > 0));
    }

    private static string Percent(decimal coverage)
    {
        return $"{(int)Math.Round(coverage * 100)}%";
    }

    /// <summary>Peakiness reads as a signed level offset — the sign is the whole message.</summary>
    private static string Signed(decimal peakiness)
    {
        return peakiness >= 0 ? $"+{peakiness:0.#}" : $"{peakiness:0.#}";
    }
}
