namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     English display names and families for the granular piucenter badge vocabulary — THE
///     label table, published because five surfaces speak it: the SRP's chips and facet cloud,
///     the coverage bars on the chart page and its dialog, the identity chips, the similar-
///     charts shelf, and the verdict sentences. It was two tables that disagreed with each
///     other ("Anchor runs" against "Anchor Runs", "90° twists" against "Twist 90") until they
///     merged here (docs/design/nuke-old-skill-categories.md §2).
///     <para>
///         Unknown keys degrade to something readable rather than to a raw key, so new
///         piucenter vocabulary needs no code change to be presentable. Values are English
///         keys for <c>IStringLocalizer</c>: the pattern vocabulary renders English in every
///         locale by long-standing ruling, and routing it through the localizer anyway leaves
///         translating it a resx change rather than a code change.
///     </para>
/// </summary>
public static class BadgeLabels
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

    /// <summary>
    ///     <c>yog_walk</c> → <c>Yog Walk</c>. Underscore is piucenter's key separator and
    ///     becomes a space; a HYPHEN is punctuation the term itself owns — <c>cross-pad</c>,
    ///     <c>co-op</c>, <c>5-stair</c> — so it survives rather than being split into two
    ///     words the community does not use.
    /// </summary>
    public static string DisplayName(string badgeKey)
    {
        if (DisplayNames.TryGetValue(badgeKey, out var known)) return known;

        var words = badgeKey.Split('_', ' ')
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
