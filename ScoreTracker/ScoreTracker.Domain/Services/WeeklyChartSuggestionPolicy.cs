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

    public static IEnumerable<Chart> GetSuggestedCharts(IEnumerable<Chart> chart, double doublesCompetitive,
        double singlesCompetitive)
    {
        var baseDoubles = (int)Math.Floor(doublesCompetitive);
        var baseSingles = (int)Math.Floor(singlesCompetitive);
        return chart.Where(c => c.Type == ChartType.CoOp ||
                                ((c.Type == ChartType.Double ? baseDoubles :
                                     c.Type == ChartType.Single ? baseSingles : baseDoubles) >= c.Level - 1 &&
                                 (c.Type == ChartType.Double ? baseDoubles :
                                     c.Type == ChartType.Single ? baseSingles : baseDoubles) <= c.Level + 2));
    }
}
