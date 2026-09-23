using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.SharedKernel.Enums;

public enum ChartType
{
    [Description("S")] Single,
    [Description("D")] Double,
    [Description("SP")] SinglePerformance,
    [Description("DP")] DoublePerformance,
    [Description("CoOp")] CoOp,

    // The six-panel pad layout: the Infinity/Pro line's HD charts, all removed by XX,
    // and RISE's 6K H.DOUBLE. Routine deliberately has NO value here — it collapses
    // onto CoOp with a real difficulty level (docs/design/legacy-mixes.md).
    [Description("HD")] HalfDouble
}

[ExcludeFromCodeCoverage]
public static class ChartTypeHelperMethods
{
    public static ChartType ParseChartTypeShortHand(string shortHand)
    {
        if (shortHand.Equals("c", StringComparison.OrdinalIgnoreCase) ||
            shortHand.Equals("CoOp", StringComparison.OrdinalIgnoreCase)) return ChartType.CoOp;
        // The shorthand became HD on 2026-09-22 (docs/design/rise.md §3.1). HDB still
        // parses so a spreadsheet, a saved preset or a link written before it still reads.
        if (shortHand.Equals("HDB", StringComparison.OrdinalIgnoreCase)) return ChartType.HalfDouble;
        foreach (var field in typeof(ChartType).GetFields())
            if (Attribute.GetCustomAttribute(field,
                    typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description.Equals(shortHand, StringComparison.OrdinalIgnoreCase))
                    return (ChartType)(field.GetValue(null) ?? ChartType.Single);
            }
            else
            {
                if (field.Name.Equals(shortHand, StringComparison.OrdinalIgnoreCase))
                    return (ChartType)(field.GetValue(null) ?? ChartType.Single);
            }

        throw new ArgumentException($"Invalid chart type short hand {shortHand}", nameof(shortHand));
    }

    public static string GetShortHand(this ChartType enumValue)
    {
        return typeof(ChartType).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? string.Empty;
    }
}
