using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Web.TypeConverters;

public class ChartTypeConverter : TypeConverter
{
    private static readonly Regex ChartRegex = new(@"(.*) ([A-Za-z]+)([0-9]+)", RegexOptions.Compiled);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string valueString)
            return ChartFromString(valueString);

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context,
        CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Chart chart) return StringFromChart(chart);
        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static string StringFromChart(Chart chart)
    {
        return $"{chart.SongName} {chart.DifficultyString}";
    }

    private static Chart ChartFromString(string chartString)
    {
        var parse = ChartRegex.Match(chartString);
        var songName = parse.Groups[1].Value;
        var type = parse.Groups[2].Value;
        var level = parse.Groups[3].Value;

        return new Chart(songName, ChartTypeHelperMethods.ParseChartTypeShortHand(type),
            DifficultyLevel.TryParse(level, out var result) ? result : 1);
    }
}