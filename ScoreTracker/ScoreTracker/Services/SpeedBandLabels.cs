using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How fast a chart is for its folder, as the Speed tier list stores it
///     (docs/design/chart-identity.md §2). The list borrows <see cref="TierListCategory" /> for
///     its ORDER only — nothing about a band is a difficulty judgement — so this is the one place
///     that borrowing is undone, and the tier lists, the chart page and the chart dialog all read
///     it here rather than each deciding for themselves what Medium means on the Speed list.
/// </summary>
public static class SpeedBandLabels
{
    /// <summary>The stored list name. Not published through api/v2 — the Popularity precedent.</summary>
    public const string ListName = "Speed";

    /// <summary>
    ///     The band's rung, 0 (slowest) to 4. Reading the category as a difficulty anywhere else
    ///     is what would put the speed and difficulty ramps back in contact.
    /// </summary>
    public static int IndexOf(TierListCategory band)
    {
        return band switch
        {
            TierListCategory.Overrated => 0,
            TierListCategory.VeryEasy => 1,
            TierListCategory.Medium => 2,
            TierListCategory.Hard => 3,
            _ => 4
        };
    }

    /// <summary>
    ///     The band's localization key. "Mid Tempo" rather than the obvious "Moderate": that key
    ///     is already the comment-moderation button, so the band rendered 관리 ("manage") in Korean
    ///     and its equivalent elsewhere. English is the key text, which is why nothing looked
    ///     wrong in English.
    /// </summary>
    public static string KeyOf(TierListCategory band)
    {
        return band switch
        {
            TierListCategory.Overrated => "Very Slow",
            TierListCategory.VeryEasy => "Slow",
            TierListCategory.Medium => "Mid Tempo",
            TierListCategory.Hard => "Fast",
            _ => "Very Fast"
        };
    }

    /// <summary>
    ///     Whether the band is one the identity engine would have claimed on its own. Only the
    ///     outer two are claims; the middle three are measurements, which is why a card shows
    ///     nothing for them and the detail surfaces file them under Features.
    /// </summary>
    public static bool IsClaim(TierListCategory band)
    {
        return IndexOf(band) is 0 or 4;
    }
}
