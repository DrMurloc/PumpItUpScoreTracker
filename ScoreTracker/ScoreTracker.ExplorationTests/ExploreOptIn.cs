namespace ScoreTracker.ExplorationTests;

/// <summary>
///     The second key on a probe that spends money or posts to a remote: on top of its secrets, it
///     runs only when the <c>PIU_EXPLORE</c> environment variable, a comma-separated list, names its
///     area (<c>PIU_EXPLORE=discord,livesite</c>). Configured secrets alone never start one, so a
///     machine set up for one probe does not run the others.
/// </summary>
internal static class ExploreOptIn
{
    public const string Variable = "PIU_EXPLORE";

    public const string Translations = "translations";
    public const string Discord = "discord";
    public const string LiveSite = "livesite";

    /// <summary>True when <c>PIU_EXPLORE</c> lists <paramref name="area" />, ignoring case and spaces.</summary>
    public static bool Names(string area)
    {
        return (Environment.GetEnvironmentVariable(Variable) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(area, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The skip reason for a probe whose area <c>PIU_EXPLORE</c> does not name.</summary>
    public static string SkipReason(string probe, string area)
    {
        return $"{probe}: opt in by setting the {Variable} environment variable to a comma-separated " +
               $"list that names '{area}' (e.g. {Variable}={area}), on top of its secrets.";
    }
}
