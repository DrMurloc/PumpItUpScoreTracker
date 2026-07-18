using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Which charts sit in a player's "worth attempting this week" band, and how weekly
///     entries turn into placements. Shared between the Weekly Challenge rotation, the
///     recommendations flow, and the weekly board page — domain policies, deliberately
///     outside any vertical.
/// </summary>
public static class WeeklyChartSuggestionPolicy
{
    public static IEnumerable<(int, WeeklyTournamentEntry)> ProcessIntoPlaces(
        IEnumerable<WeeklyTournamentEntry> entries)
    {
        var place = 1;
        foreach (var scoreGroup in entries.GroupBy(e => e.Score).OrderByDescending(g => g.Key))
        {
            foreach (var score in scoreGroup) yield return (place, score);
            place += scoreGroup.Count();
        }
    }

    // Limbo Day inverts the board (Daily Step): only passing (non-broken) runs qualify, and the
    // LOWEST score wins. Ties share a place and the next place jumps by the tie count — the same
    // laddering as ProcessIntoPlaces, just ascending.
    public static IEnumerable<(int, WeeklyTournamentEntry)> ProcessIntoPlacesAscending(
        IEnumerable<WeeklyTournamentEntry> entries)
    {
        var place = 1;
        foreach (var scoreGroup in entries.Where(e => !e.IsBroken).GroupBy(e => e.Score).OrderBy(g => g.Key))
        {
            foreach (var score in scoreGroup) yield return (place, score);
            place += scoreGroup.Count();
        }
    }

    public static IEnumerable<Chart> GetSuggestedCharts(IEnumerable<Chart> chart, double doublesCompetitive,
        double singlesCompetitive)
    {
        return chart.Where(c => IsWithinRange(c,
            c.Type == ChartType.Single ? singlesCompetitive : doublesCompetitive));
    }

    /// <summary>
    ///     Whether a competitive level sits in a chart's "worth attempting" band:
    ///     floor(level) within [chart level − 1, chart level + 2]; co-op charts are always in.
    ///     One definition for suggestions and the relevant-players board filter — the two
    ///     must never disagree on what "in range" means.
    /// </summary>
    public static bool IsWithinRange(Chart chart, double competitiveLevel)
    {
        if (chart.Type == ChartType.CoOp) return true;
        var floor = (int)Math.Floor(competitiveLevel);
        return floor >= chart.Level - 1 && floor <= chart.Level + 2;
    }
}
