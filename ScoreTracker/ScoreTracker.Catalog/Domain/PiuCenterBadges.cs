using ScoreTracker.Catalog.Contracts;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     English display names for the granular piucenter badge vocabulary — the one label
///     table for every chart surface (the SRP facet and chips, the coverage bars on the
///     chart page and its dialog). Unknown keys fall back to Title Case so new piucenter
///     vocabulary degrades to something readable without a code change; the UI layer
///     localizes. Colour families used to ride the rollup's category buckets and went with
///     them (docs/design/nuke-old-skill-categories.md) — a badge chip is neutral and its
///     printed name carries the meaning.
/// </summary>
internal static class PiuCenterBadges
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = "Runs",
            ["anchor_run"] = "Anchor Runs",
            ["run_without_twists"] = "Runs Without Twists",
            ["drill"] = "Drills",
            ["jump"] = "Jumps",
            ["jack"] = "Jacks",
            ["bracket"] = "Brackets",
            ["staggered_bracket"] = "Staggered Brackets",
            ["bracket_run"] = "Bracket Runs",
            ["bracket_drill"] = "Bracket Drills",
            ["bracket_jump"] = "Bracket Jumps",
            ["bracket_twist"] = "Bracket Twists",
            ["twists"] = "Twists",
            ["twist_90"] = "Twist 90",
            ["twist_over90"] = "Over-90 Twists",
            ["twist_close"] = "Close Twists",
            ["twist_far"] = "Far Twists",
            ["mid6_doubles"] = "Mid-6 Doubles",
            ["mid4_doubles"] = "Mid-4 Doubles",
            ["sustained"] = "Sustained",
            ["bursty"] = "Bursty",
            ["footswitch"] = "Footswitches",
            ["hold_footswitch"] = "Hold Footswitches",
            ["hold_footslide"] = "Hold Footslides",
            ["5-stair"] = "5-Stairs",
            ["10-stair"] = "10-Stairs",
            ["yog_walk"] = "Yog Walks",
            ["cross-pad_transition"] = "Cross-pad Transitions",
            ["co-op_pad_transition"] = "Co-op Pad Transitions",
            ["split"] = "Splits",
            ["hands"] = "Hands",
            ["doublestep"] = "Doublesteps",
            ["side3_singles"] = "Side-3 Singles"
        };

    public static string DisplayName(string badgeKey)
    {
        if (DisplayNames.TryGetValue(badgeKey, out var known)) return known;

        var words = badgeKey.Split('_', '-', ' ')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(' ', words);
    }

    /// <summary>
    ///     Owner-defined families (2026-07-26), one per badge, all 33 accounted for. The calls
    ///     worth naming because they are not obvious: jacks and jumps are Tech rather than
    ///     stamina, side-3 singles are a Twists problem, and everything that lives across the
    ///     far pad — 10-stairs, transitions, mid-4/6, splits, yog walks — is its own Doubles
    ///     Tech family rather than being folded into Tech.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, BadgeCategory> Categories =
        new Dictionary<string, BadgeCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["bracket"] = BadgeCategory.Brackets,
            ["staggered_bracket"] = BadgeCategory.Brackets,
            ["bracket_run"] = BadgeCategory.Brackets,
            ["bracket_drill"] = BadgeCategory.Brackets,
            ["bracket_jump"] = BadgeCategory.Brackets,
            ["bracket_twist"] = BadgeCategory.Brackets,

            ["twists"] = BadgeCategory.Twists,
            ["twist_90"] = BadgeCategory.Twists,
            ["twist_over90"] = BadgeCategory.Twists,
            ["twist_close"] = BadgeCategory.Twists,
            ["twist_far"] = BadgeCategory.Twists,
            ["side3_singles"] = BadgeCategory.Twists,

            ["run"] = BadgeCategory.StaminaAndRuns,
            ["anchor_run"] = BadgeCategory.StaminaAndRuns,
            ["run_without_twists"] = BadgeCategory.StaminaAndRuns,
            ["drill"] = BadgeCategory.StaminaAndRuns,
            ["sustained"] = BadgeCategory.StaminaAndRuns,
            ["bursty"] = BadgeCategory.StaminaAndRuns,

            ["jack"] = BadgeCategory.Tech,
            ["jump"] = BadgeCategory.Tech,
            ["footswitch"] = BadgeCategory.Tech,
            ["hold_footswitch"] = BadgeCategory.Tech,
            ["hold_footslide"] = BadgeCategory.Tech,
            ["5-stair"] = BadgeCategory.Tech,
            ["hands"] = BadgeCategory.Tech,
            ["doublestep"] = BadgeCategory.Tech,

            ["10-stair"] = BadgeCategory.DoublesTech,
            ["mid4_doubles"] = BadgeCategory.DoublesTech,
            ["mid6_doubles"] = BadgeCategory.DoublesTech,
            ["split"] = BadgeCategory.DoublesTech,
            ["yog_walk"] = BadgeCategory.DoublesTech,
            ["cross-pad_transition"] = BadgeCategory.DoublesTech,
            ["co-op_pad_transition"] = BadgeCategory.DoublesTech
        };

    /// <summary>Null only for a badge piucenter adds that this table has not learned yet.</summary>
    public static BadgeCategory? CategoryFor(string badgeKey)
    {
        return Categories.TryGetValue(badgeKey, out var category) ? category : null;
    }

    /// <summary>Every badge with a display name, so a test can prove none lacks a family.</summary>
    public static IReadOnlyCollection<string> KnownBadges => DisplayNames.Keys.ToArray();

}
