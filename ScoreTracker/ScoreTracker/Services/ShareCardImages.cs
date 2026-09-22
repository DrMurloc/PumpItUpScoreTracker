using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The image URLs a share card is built from. The renderer fetches them itself, so every
///     caller has to spell them the same way — they were spelled twice before the peers page
///     grew a Download button of its own. Which folder a mix draws from is the mix's profile's
///     answer (<see cref="MixArt" />), never a guess off its scoring model.
/// </summary>
public static class ShareCardImages
{
    private const string Root = "https://piuimages.arroweclip.se";

    /// <summary>
    ///     The difficulty bubble, spelled exactly as <c>DifficultyBubble</c> spells it on the
    ///     page. SP/DP predate per-mix art and are served flat everywhere; otherwise the mix's
    ///     profile names its bubble folder, null meaning the flat XX set every legacy mix draws.
    /// </summary>
    public static string DifficultyBubble(MixEnum? mix, ChartType chartType, string difficultyString)
    {
        var folder = chartType is ChartType.SinglePerformance or ChartType.DoublePerformance || mix == null
            ? null
            : MixProfiles.For(mix.Value).Art.BubbleFolder;
        var file = BubbleFileName(difficultyString);
        return folder == null
            ? $"{Root}/difficulty/{file}.png"
            : $"{Root}/difficulty/{folder}/{file}.png";
    }

    /// <summary>
    ///     The bubble's file stem. It is the difficulty shorthand, except that the 25 half-double
    ///     stepballs were uploaded as <c>hdb4</c>…<c>hdb28</c> before the shorthand became HD
    ///     (docs/design/rise.md D12) — an asset path no player reads was not worth a re-upload
    ///     and a CDN purge, so the name is translated here instead.
    /// </summary>
    private static string BubbleFileName(string difficultyString)
    {
        var lower = difficultyString.ToLower();
        return lower.StartsWith("hd", StringComparison.Ordinal) && !lower.StartsWith("hdb", StringComparison.Ordinal)
            ? $"hdb{lower[2..]}"
            : lower;
    }

    public static string DifficultyBubble(MixEnum mix, ChartType chartType, DifficultyLevel level) =>
        DifficultyBubble(mix, chartType, DifficultyLevel.ToShorthand(chartType, level));

    /// <summary>
    ///     One chart's bubble, or null where the page renders a legacy chip instead: pre-Exceed
    ///     slots, a half-double on a mix whose bubble set has no H. DOUBLE stepball, and levelled
    ///     legacy co-ops have no bubble art, so a card that drew one would be inventing it.
    /// </summary>
    public static string? DifficultyBubble(Chart chart) =>
        chart.Slot != null ||
        (chart.Type == ChartType.HalfDouble && !MixProfiles.For(chart.Mix).Art.HasHalfDoubleBubble) ||
        (chart.Type == ChartType.CoOp && chart.Mix.UsesLegacyScoring() && chart.Level != chart.PlayerCount)
            ? null
            : DifficultyBubble(chart.Mix, chart.Type, chart.DifficultyString);

    /// <summary>The letter, from the mix's own letter set where it has one and the flat Phoenix set otherwise.</summary>
    public static string LetterGrade(PhoenixLetterGrade grade, bool isBroken, MixEnum? mix = null)
    {
        var folder = mix == null ? null : MixProfiles.For(mix.Value).Art.ScoreArtFolder;
        var file = $"{grade.ToString().ToLower()}{(isBroken ? "_broken" : "")}.png";
        return folder == null ? $"{Root}/letters/{file}" : $"{Root}/letters/{folder}/{file}";
    }

    /// <summary>
    ///     The plate — or the mark a mix shows in its place — from the mix's own set where it has
    ///     one. The file is always named by the stored plate's shorthand, whatever the mix calls it.
    /// </summary>
    public static string Plate(PhoenixPlate plate, MixEnum? mix = null)
    {
        var folder = mix == null ? null : MixProfiles.For(mix.Value).Art.ScoreArtFolder;
        var file = $"{plate.GetShorthand().ToLower()}.png";
        return folder == null ? $"{Root}/plates/{file}" : $"{Root}/plates/{folder}/{file}";
    }
}
