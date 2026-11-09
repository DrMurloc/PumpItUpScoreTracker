using ScoreTracker.ChartIntelligence.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     When the tier list's community PUMBILITY list says how thin its population is
///     (docs/design/rise.md D33): it counts full top 50s, and under the PUMBILITY calculator's own
///     "Early days" cutoff the page names how many are behind it. No Phoenix list is ever that thin.
/// </summary>
public static class PumbilityEarlyDays
{
    /// <param name="personalized">Whether the list is the viewer's personalized one, which names its peers instead.</param>
    /// <param name="lens">The tier list's lens; only PUMBILITY counts pools.</param>
    /// <param name="pools">How many full top 50s the community list was counted over.</param>
    public static bool Applies(bool personalized, string lens, int pools)
    {
        return !personalized && lens == "PUMBILITY" && pools > 0 &&
               pools < PumbilityPoolCompositionRecord.EarlyDaysPools;
    }
}
