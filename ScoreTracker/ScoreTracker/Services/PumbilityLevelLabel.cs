using ScoreTracker.Domain.Models.Titles.Phoenix2;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How a rung of the Phoenix 2 PUMBILITY level ladder is named on the page: the gem plus its
///     level ("[P.B] DIAMOND LV.4"), the capstone by its gem alone. Gem names are the game's own
///     proper nouns and never localize; only rung 0, which the game names nowhere, takes a word
///     of ours — the caller passes it localized. One place, so the badge, the peer line and the
///     breakdown page cannot spell the same rung three ways.
/// </summary>
public static class PumbilityLevelLabel
{
    public static string Of(Phoenix2PumbilityLevel rung, string unranked)
    {
        if (rung.Gem is not { } gem) return unranked;
        return rung.Level is { } level ? $"{gem} LV.{level}" : gem.ToString();
    }
}
