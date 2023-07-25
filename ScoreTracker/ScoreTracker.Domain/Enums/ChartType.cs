using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum ChartType
{
    [Description("S")] Single,
    [Description("D")] Double,
    [Description("SP")] SinglePerformance,
    [Description("DP")] DoublePerformance,
    [Description("CoOp")] CoOp
}

public static class ChartTypeHelperMethods
{
    public static ChartType ParseChartTypeShortHand(string shortHand)
    {
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