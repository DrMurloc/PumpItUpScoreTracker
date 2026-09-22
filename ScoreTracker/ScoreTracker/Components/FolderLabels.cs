using Microsoft.Extensions.Localization;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components;

/// <summary>
///     What a folder is called on a given mix. A folder is a category, but a category holding
///     exactly one of the mix's chart types is named after that type — RISE's doubles folder is
///     its half-doubles and reads H. DOUBLE, while Phoenix 2's reads Doubles
///     (docs/design/rise.md §3.1). Shared so the picker's button, its tabs and any page heading
///     say the same word.
/// </summary>
public static class FolderLabels
{
    public static string For(IStringLocalizer localizer, ChartTypeCategory category, MixEnum mix)
    {
        var types = category.TypesOn(mix);
        return types.Count == 1 && types[0] != category.HeadType()
            ? localizer[NameOf(types[0])]
            : localizer[NameOf(category)];
    }

    /// <summary>The folder plus its level, as the picker's button prints it.</summary>
    public static string WithLevel(IStringLocalizer localizer, ChartTypeCategory category, MixEnum mix, int level)
    {
        return category == ChartTypeCategory.CoOp
            ? $"{localizer["CoOp"]} ×{level}"
            : $"{ShortHandFor(category, mix)}{level}";
    }

    /// <summary>The shorthand the folder wears on this mix: <c>S</c>, <c>D</c>, <c>HD</c>.</summary>
    public static string ShortHandFor(ChartTypeCategory category, MixEnum mix)
    {
        var types = category.TypesOn(mix);
        return types.Count == 1 ? types[0].GetShortHand() : category.GetShortHand();
    }

    private static string NameOf(ChartTypeCategory category) => category switch
    {
        ChartTypeCategory.Single => "Singles",
        ChartTypeCategory.CoOp => "CoOp",
        _ => "Doubles"
    };

    private static string NameOf(ChartType type) => type switch
    {
        ChartType.HalfDouble => "H. DOUBLE",
        ChartType.SinglePerformance => "Single Performance",
        ChartType.DoublePerformance => "Double Performance",
        ChartType.Single => "Singles",
        ChartType.CoOp => "CoOp",
        _ => "Doubles"
    };
}
