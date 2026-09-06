using Microsoft.Extensions.Localization;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The small vocabulary the March of Murlocs pages share: routes, the clock formats a
///     session prints (downtime as m:ss, song time as h:mm:ss), and ordinals. An ordinal is a
///     localized format per suffix class rather than an English suffix glued onto a number, so
///     a locale that writes 1위 for every place translates four keys and is done.
/// </summary>
public static class MoMText
{
    public const string SeasonRoute = "/MarchOfMurlocs";
    public const string PlannerRoute = "/TournamentBuilder";

    /// <summary>The rules of record (docs/design/march-of-murlocs.md §11.11, D42): static, in the sitemap.</summary>
    public const string RulesRoute = "/MarchOfMurlocs/Rules";
    public const string RulesUrl =
        "https://docs.google.com/document/d/1Nwr-PDy6lgkTSt4dKu1-0fdeDXdgLWvl7j5yiuIcRCw/edit?usp=sharing";

    public static string SeasonPath(Guid seasonId) => $"{SeasonRoute}/{seasonId}";
    public static string SessionPath(Guid sessionId) => $"{SeasonRoute}/Session/{sessionId}";

    /// <summary>The old record page, reachable behind the section's links until Slice 4b replaces it.</summary>
    public static string RecordPath(Guid boardId) => $"/Tournament/Stamina/{boardId}/Record";

    public static string Ordinal(int place, IStringLocalizer<App> localizer)
    {
        return localizer[OrdinalKey(place), place];
    }

    /// <summary>The English suffix alone, for the big place figure that sets it in a smaller face.</summary>
    public static string OrdinalSuffix(int place)
    {
        return OrdinalKey(place)[3..];
    }

    private static string OrdinalKey(int place)
    {
        var tens = place % 100;
        if (tens is >= 11 and <= 13) return "{0}th";
        return (place % 10) switch
        {
            1 => "{0}st",
            2 => "{0}nd",
            3 => "{0}rd",
            _ => "{0}th"
        };
    }

    public static string MinutesSeconds(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    public static string HoursMinutesSeconds(TimeSpan time)
    {
        return $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
    }

    /// <summary>"2 Feb – 31 Mar 2025": the season's span in the reader's culture, the year once.</summary>
    public static string DateSpan(DateTimeOffset start, DateTimeOffset end)
    {
        return start.Year == end.Year
            ? $"{start:d MMM} – {end:d MMM yyyy}"
            : $"{start:d MMM yyyy} – {end:d MMM yyyy}";
    }
}
