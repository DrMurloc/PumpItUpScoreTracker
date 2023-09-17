using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum PhoenixPlate
{
    [PlateShorthandAttribute("RG")] [Description("Rough Game")]
    RoughGame,

    [PlateShorthandAttribute("FG")] [Description("Fair Game")]
    FairGame,

    [PlateShorthandAttribute("TG")] [Description("Talented Game")]
    TalentedGame,

    [PlateShorthandAttribute("MG")] [Description("Marvelous Game")]
    MarvelousGame,

    [PlateShorthandAttribute("SG")] [Description("Superb Game")]
    SuperbGame,

    [PlateShorthandAttribute("EG")] [Description("Extreme Game")]
    ExtremeGame,

    [PlateShorthandAttribute("UG")] [Description("Ultimate Game")]
    UltimateGame,

    [PlateShorthandAttribute("PG")] [Description("Perfect Game")]
    PerfectGame
}

internal sealed class PlateShorthandAttribute : Attribute
{
    public PlateShorthandAttribute(string shorthand)
    {
        Shorthand = shorthand;
    }

    public string Shorthand { get; }
}

public static class PhoenixPlateHelperMethods
{
    private static readonly IDictionary<string, PhoenixPlate> Parser =
        Enum.GetValues<PhoenixPlate>().ToDictionary(e => e.GetName());

    public static string GetShorthand(this PhoenixPlate enumValue)
    {
        return typeof(PhoenixPlate).GetField(enumValue.ToString())?.GetCustomAttribute<PlateShorthandAttribute>()
            ?.Shorthand ?? enumValue.ToString();
    }

    public static string GetName(this PhoenixPlate enumValue)
    {
        return typeof(PhoenixPlate).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }

    public static PhoenixPlate? TryParse(string? value)
    {
        return value == null ? null : Parser.ContainsKey(value) ? Parser[value] : null;
    }
}