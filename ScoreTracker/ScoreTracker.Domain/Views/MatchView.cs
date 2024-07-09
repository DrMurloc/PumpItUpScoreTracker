using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views;

public sealed record MatchView(Name MatchName, Name PhaseName, int MatchOrder, int ChartCount, Name RandomSettings,
    MatchState State,
    Name[] Players,
    Guid[] ActiveCharts,
    Guid[] VetoedCharts, Guid[] ProtectedCharts, IDictionary<string, PhoenixScore[]> Scores,
    IDictionary<string, int[]> Points, Name[] FinalPlaces, int Round = 1, string Machine = "",
    int[]? PointsPerPlace = null)
{
    public MatchView CalculatePoints()
    {
        var match = this with { };

        var scoring = match.Players.Length == 2
            ? new[] { 1, 0 }
            : match.PointsPerPlace ??
              match.Players.Select((p, i) => i + 1).OrderByDescending(i => i).ToArray();

        for (var chartIndex = 0; chartIndex < match.ActiveCharts.Length; chartIndex++)
        {
            var pointIndex = 0;
            foreach (var scoreGroup in match.Players.GroupBy(p => (int)match.Scores[p][chartIndex])
                         .OrderByDescending(g => g.Key))
            {
                var points = scoreGroup.Key == 0 ? 0 : scoring[pointIndex];
                foreach (var player in scoreGroup) match.Points[player][chartIndex] = points;

                pointIndex += scoreGroup.Count();
            }
        }

        var currentPosition = 0;

        foreach (var tie in match.Players.GroupBy(p => match.Points[p].Sum())
                     .OrderByDescending(g => g.Key))
        foreach (var tieBreakerResult in tie.OrderByDescending(name => match.Scores[name].Sum(s => s)))
            match.FinalPlaces[currentPosition++] = tieBreakerResult;
        return match;
    }
}