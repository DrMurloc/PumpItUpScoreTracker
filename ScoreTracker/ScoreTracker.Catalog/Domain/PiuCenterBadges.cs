using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     English display names and color families for the granular piucenter badge
///     vocabulary (the SRP skills facet — the rollup <see cref="Skill" /> enum is not
///     consumed there). Unknown keys fall back to Title Case so new piucenter vocabulary
///     degrades to something readable without a code change; the UI layer localizes.
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

    /// <summary>Color family via the badge's rollup mapping; null for unmapped badges (neutral chip).</summary>
    public static SkillCategory? CategoryFor(string badgeKey)
    {
        var mapped = PiuCenterSkillMapper.MapTheirSkill(badgeKey);
        return mapped.Count == 0 ? null : mapped[0].GetPrimaryCategory();
    }
}
