using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.MoM;

/// <summary>
///     The March of Murlocs URL family (docs/design/march-of-murlocs.md §11.1): quarterly
///     seasons route by year and season word (Winter/Spring/Summer/Fall = Q1–Q4), the three
///     off-grid legacy seasons by hyphenated name, and a board by its unlocalized chart-type
///     segment. Bare /MarchOfMurlocs serves the live season and canonicalizes to its dated
///     board URL — a crawled URL never moves when a season ends.
/// </summary>
public static class MoMRoutes
{
    public const string Root = "/MarchOfMurlocs";

    private static readonly string[] SeasonWords = { "Winter", "Spring", "Summer", "Fall" };

    public static string? SeasonWord(int quarter)
    {
        return quarter is >= 1 and <= 4 ? SeasonWords[quarter - 1] : null;
    }

    public static bool TryParseSeasonWord(string word, out int quarter)
    {
        for (var i = 0; i < SeasonWords.Length; i++)
            if (string.Equals(SeasonWords[i], word, StringComparison.OrdinalIgnoreCase))
            {
                quarter = i + 1;
                return true;
            }

        quarter = 0;
        return false;
    }

    public static string TypeSlug(ChartType type)
    {
        return type == ChartType.Single ? "Singles" : "Doubles";
    }

    public static bool TryParseTypeSlug(string slug, out ChartType type)
    {
        if (string.Equals(slug, "Singles", StringComparison.OrdinalIgnoreCase))
        {
            type = ChartType.Single;
            return true;
        }

        if (string.Equals(slug, "Doubles", StringComparison.OrdinalIgnoreCase))
        {
            type = ChartType.Double;
            return true;
        }

        type = default;
        return false;
    }

    public static string SeasonPath(MoMSeasonRef season)
    {
        return season is { Year: not null, Quarter: not null }
            ? $"{Root}/{season.Year}/{SeasonWord(season.Quarter.Value)}"
            : $"{Root}/{Uri.EscapeDataString(season.Name.Replace(' ', '-'))}";
    }

    public static string BoardPath(MoMSeasonRef season, ChartType type)
    {
        return $"{SeasonPath(season)}/{TypeSlug(type)}";
    }

    public static string SessionPath(Guid sessionId)
    {
        return $"{Root}/Session/{sessionId}";
    }

    public static string EditPath(Guid sessionId)
    {
        return $"{Root}/Session/{sessionId}/Edit";
    }

    /// <summary>The create-a-draft entry: Submit resolves or creates the draft and lands on its real URL.</summary>
    public static string NewSessionPath(Guid boardId)
    {
        return $"{Root}/Session/New/Edit?Board={boardId}";
    }

    public const string PlannerPath = $"{Root}/Planner";

    public const string RulesUrl =
        "https://docs.google.com/document/d/1Nwr-PDy6lgkTSt4dKu1-0fdeDXdgLWvl7j5yiuIcRCw/edit";
}
