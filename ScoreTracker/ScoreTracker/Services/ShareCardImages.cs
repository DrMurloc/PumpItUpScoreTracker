using ScoreTracker.SharedKernel.Enums;
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

    public static string DifficultyBubble(MixEnum mix, ChartType chartType, DifficultyLevel level) =>
        $"{Root}/difficulty/{mix}/{chartType.GetShortHand().ToLower()}{level}.png";

    public static string LetterGrade(PhoenixLetterGrade grade, bool isBroken) =>
        $"{Root}/letters/{grade.ToString().ToLower()}{(isBroken ? "_broken" : "")}.png";

    public static string Plate(PhoenixPlate plate) => $"{Root}/plates/{plate.GetShorthand().ToLower()}.png";
}
