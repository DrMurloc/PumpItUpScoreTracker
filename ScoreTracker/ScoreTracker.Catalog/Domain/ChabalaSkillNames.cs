namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The retired hand-tag vocabulary, frozen. These are the names the pre-crawler skill tags
///     were stored under, and the only place they are still spoken is the Chabala tier list —
///     his list, his words (docs/design/nuke-old-skill-categories.md §7).
///     <para>
///         A table rather than an enum on purpose: the enum is gone, the rows are not, and
///         nothing may map these onto the badge families. An unknown name renders as stored.
///     </para>
/// </summary>
internal static class ChabalaSkillNames
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VeryFast"] = "Very Fast",
            ["Fast"] = "Fast",
            ["Moderate"] = "Moderate",
            ["Slow"] = "Slow",
            ["EndRun"] = "End Run",
            ["Stamina"] = "Stamina",
            ["Twists"] = "Twists",
            ["Technical"] = "Technical",
            ["HalfDouble"] = "Half-Double",
            ["Brackets"] = "Brackets",
            ["Jumps"] = "Jumps",
            ["Bursts"] = "Bursts",
            ["Drills"] = "Drills",
            ["BracketsAndRuns"] = "Brackets & Runs",
            ["Jacks"] = "Jacks",
            ["Gimmicks"] = "Gimmicks",
            ["Runs"] = "Runs"
        };

    public static string DisplayName(string storedName)
    {
        return DisplayNames.TryGetValue(storedName, out var known) ? known : storedName;
    }
}
