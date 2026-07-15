namespace ScoreTracker.Web.Services;

/// <summary>
///     Human names for piucenter's raw badge vocabulary — the similar-charts shelf says
///     what a pair actually matched on ("Brackets 50%"), never "skills match", which
///     reads as broken (docs/design/chart-similarity.md §4).
///     Deliberately its own table rather than a hop through <c>Skill</c>: that vocabulary
///     is a lossy display projection (it maxes across mapped badges, applies per-badge
///     thresholds, and drops <c>doublestep</c> and <c>side3_singles</c> entirely), and it
///     is slated for a rename or a deletion. A label table is what lets that happen
///     without touching the shelf. Values are English keys for <c>IStringLocalizer</c>.
///     The table picks one name per badge without claiming it is the name — the community
///     calls most of these several things — so it exists to be readable and stable, not
///     canonical. An unmapped badge is humanized rather than dropped, so a new piucenter
///     badge degrades to "Yog Walk" instead of to `yog_walk` or to nothing.
///     These keys are deliberately **not** in the resx catalogues, so every locale renders
///     the English term — which is exactly what the site already does with the whole skill
///     vocabulary (`Skill.GetName()` never passes through the localizer, so "Brackets" and
///     "Twists" are English in Korean and Japanese today). Routing them through
///     <c>L[…]</c> anyway costs nothing and leaves the hook in place, so translating the
///     pattern vocabulary later is a resx change and not a code change.
/// </summary>
internal static class SimilarityBadgeLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bracket"] = "Brackets",
            ["bracket_drill"] = "Bracket drills",
            ["bracket_jump"] = "Bracket jumps",
            ["bracket_run"] = "Bracket runs",
            ["bracket_twist"] = "Bracket twists",
            ["staggered_bracket"] = "Staggered brackets",
            ["run"] = "Runs",
            ["anchor_run"] = "Anchor runs",
            ["drill"] = "Drills",
            ["jump"] = "Jumps",
            ["jack"] = "Jacks",
            ["doublestep"] = "Double steps",
            ["footswitch"] = "Footswitches",
            ["hold_footswitch"] = "Hold footswitches",
            ["hold_footslide"] = "Hold footslides",
            ["twist_90"] = "90° twists",
            ["twist_over90"] = "Over-90° twists",
            ["twist_close"] = "Close twists",
            ["twist_far"] = "Far twists",
            ["5-stair"] = "5-stairs",
            ["10-stair"] = "10-stairs",
            ["side3_singles"] = "Side-3 singles",
            ["mid4_doubles"] = "Mid-4 doubles",
            ["mid6_doubles"] = "Mid-6 doubles",
            ["cross-pad_transition"] = "Cross-pad transitions",
            ["co-op_pad_transition"] = "Co-op pad transitions",
            ["yog_walk"] = "YOG walks",
            ["split"] = "Splits",
            ["hands"] = "Hands"
        };

    public static string For(string badge)
    {
        return Labels.TryGetValue(badge, out var label) ? label : Humanize(badge);
    }

    /// <summary>
    ///     <c>yog_walk</c> → <c>Yog Walk</c>. Underscores are piucenter's key separator, not
    ///     something a reader should ever see; a hyphen is punctuation the term itself owns
    ///     (<c>cross-pad</c>, <c>5-stair</c>) and survives.
    /// </summary>
    private static string Humanize(string badge)
    {
        var words = badge.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(' ', words);
    }
}
