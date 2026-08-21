using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The image URLs a share card is built from. The renderer fetches them itself, so every
///     caller has to spell them the same way — they were spelled twice before the peers page
///     grew a Download button of its own.
/// </summary>
public static class ShareCardImages
{
    private const string Root = "https://piuimages.arroweclip.se";

    /// <summary>
    ///     The difficulty bubble, spelled exactly as <c>DifficultyBubble</c> spells it on the
    ///     page. Modern mixes keep their per-mix art; SP/DP predate it and every legacy mix — XX
    ///     included — reuses the XX set, so both are served flat.
    /// </summary>
    public static string DifficultyBubble(MixEnum? mix, ChartType chartType, string difficultyString) =>
        chartType is ChartType.SinglePerformance or ChartType.DoublePerformance ||
        mix?.UsesLegacyScoring() == true
            ? $"{Root}/difficulty/{difficultyString.ToLower()}.png"
            : $"{Root}/difficulty/{mix}/{difficultyString.ToLower()}.png";

    public static string DifficultyBubble(MixEnum mix, ChartType chartType, DifficultyLevel level) =>
        DifficultyBubble(mix, chartType, DifficultyLevel.ToShorthand(chartType, level));

    /// <summary>
    ///     One chart's bubble, or null where the page renders a legacy chip instead: pre-Exceed
    ///     slots, Half-Double and levelled legacy co-ops have no bubble art in any set, so a card
    ///     that drew one would be inventing it.
    /// </summary>
    public static string? DifficultyBubble(Chart chart) =>
        chart.Slot != null || chart.Type == ChartType.HalfDouble ||
        (chart.Type == ChartType.CoOp && chart.Mix.UsesLegacyScoring() && chart.Level != chart.PlayerCount)
            ? null
            : DifficultyBubble(chart.Mix, chart.Type, chart.DifficultyString);

    public static string LetterGrade(PhoenixLetterGrade grade, bool isBroken) =>
        $"{Root}/letters/{grade.ToString().ToLower()}{(isBroken ? "_broken" : "")}.png";

    public static string Plate(PhoenixPlate plate) => $"{Root}/plates/{plate.GetShorthand().ToLower()}.png";
}
