using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Whether a chart's official ranking can hide the players who hold it (docs/design/chart-presence-graph.md
///     §8). piugame's chart rankings stop at <see cref="Places" />, and an official-ranking player's top 50 is
///     rebuilt from them. Once a ranking is full and even its lowest score is a high one, a ranking player who
///     holds the chart with a lower score is on no ranking of it, and every count rebuilt from the rankings reads
///     them as not holding it.
/// </summary>
public static class CrowdedRanking
{
    /// <summary>How many places piugame publishes on a chart ranking.</summary>
    public const int Places = 300;

    /// <summary>
    ///     The lowest score a full ranking must still hold to hide holders: S+ through level 21, S through 23,
    ///     AAA+ above. A single reads as the double one level above it, so S20 sits with D21.
    /// </summary>
    public static int Bar(ChartType type, DifficultyLevel level)
    {
        var paired = type == ChartType.Single ? (int)level + 1 : (int)level;
        return paired <= 21 ? 975_000 : paired <= 23 ? 970_000 : 960_000;
    }

    /// <summary>True when the chart's ranking holds all its places and its lowest score is at or above the chart's bar.</summary>
    public static bool HidesHolders(Chart chart, OfficialChartRanking ranking)
    {
        return ranking.Places >= Places && ranking.LowestScore >= Bar(chart.Type, chart.Level);
    }
}
