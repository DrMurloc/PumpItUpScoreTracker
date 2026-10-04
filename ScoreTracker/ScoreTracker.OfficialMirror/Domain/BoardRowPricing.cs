using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     What one mirrored chart-board row is worth in PUMBILITY. A board row carries a score and no
///     plate, so it is priced at the plate its score most plausibly carries, and at the chart's own
///     type, because Phoenix 2 prices a single one level up the base curve.
/// </summary>
internal static class BoardRowPricing
{
    public static double Price(ScoringConfiguration scoring, ChartType type, int level, int score)
    {
        var phoenixScore = PhoenixScore.From(score);
        return scoring.GetScore(type, DifficultyLevel.From(level), phoenixScore,
            ScoringConfiguration.ExpectedPlateForScore(phoenixScore));
    }

    /// <summary>The chart type a board row was published under, or null when it does not name one.</summary>
    public static ChartType? TypeOf(string? chartType)
    {
        return Enum.TryParse<ChartType>(chartType, out var type) && Enum.IsDefined(type) ? type : null;
    }
}
